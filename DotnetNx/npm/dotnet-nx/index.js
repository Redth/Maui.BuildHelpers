// @ts-check

const childProcess = require('child_process');
const path = require('path');

const PROJECT_PATTERN = '**/*.{csproj,fsproj,vbproj}';
const DEFAULT_SELECTOR_TAG_PREFIX = 'dotnet';

function runNxdn(workspaceRoot, projectFiles, options) {
  const command = options?.nxdnPath || process.env.DOTNET_NX_NXDN || 'nxdn';
  const args = ['project-metadata', '--workspace', workspaceRoot];
  for (const projectFile of projectFiles) {
    args.push('--project', projectFile);
  }

  const result = childProcess.spawnSync(command, args, {
    cwd: workspaceRoot,
    encoding: 'utf8',
    windowsHide: true,
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    const stderr = result.stderr ? `\n${result.stderr.trim()}` : '';
    const stdout = result.stdout ? `\n${result.stdout.trim()}` : '';
    throw new Error(`nxdn project-metadata failed with exit code ${result.status}.${stderr}${stdout}`);
  }

  return JSON.parse(result.stdout);
}

function toNxNode(project, options) {
  const projectRoot = project.projectRoot && project.projectRoot !== '.'
    ? project.projectRoot
    : path.dirname(project.projectFile);
  const selectorTags = createSelectorTags(project, options || {});
  const tags = [...new Set([...(project.explicitTags || []), ...selectorTags])].sort();

  return [
    project.projectFile,
    {
      projects: {
        [projectRoot]: {
          projectType: project.projectType,
          tags,
          metadata: {
            technologies: project.technologies || ['dotnet'],
            dotnetNx: {
              schemaVersion: 2,
              capabilities: project.capabilities,
              configurations: project.configurations || [],
              targetHostRequirements: project.targetHostRequirements || [],
              explicitTags: project.explicitTags || [],
              selectorTags,
              diagnostics: project.diagnostics,
            },
          },
        },
      },
    },
  ];
}

function createSelectorTags(project, options) {
  const selectors = new Set(options?.selectorTags || []);
  const prefix = options?.selectorTagPrefix || DEFAULT_SELECTOR_TAG_PREFIX;
  const tags = new Set();
  const configurations = project.configurations || [];

  if (selectors.has('target-framework')) {
    for (const configuration of configurations) {
      if (configuration.framework?.shortName) {
        tags.add(`${prefix}:tfm:${configuration.framework.shortName}`);
      }
    }
  }

  if (selectors.has('platform')) {
    for (const configuration of configurations) {
      if (configuration.framework?.platform) {
        tags.add(`${prefix}:platform:${configuration.framework.platform}`);
      }
    }
  }

  if (selectors.has('runtime-identifier')) {
    for (const configuration of configurations) {
      for (const runtimeIdentifier of configuration.runtimeIdentifiers || []) {
        tags.add(`${prefix}:rid:${runtimeIdentifier}`);
      }
    }
  }

  if (selectors.has('host')) {
    const target = options?.hostTarget || 'build';
    const requirement = (project.targetHostRequirements || [])
      .find(candidate => candidate.target === target);
    const mayUseRequirement = requirement &&
      (requirement.source === 'explicit' || options?.includeInferredHostSelectors === true);
    if (mayUseRequirement) {
      for (const host of requirement.hosts || []) {
        tags.add(`${prefix}:host:${target}:${host}`);
      }
    }
  }

  return [...tags].sort();
}

module.exports = {
  createNodesV2: [
    PROJECT_PATTERN,
    async (projectFiles, options, context) => {
      if (!projectFiles || projectFiles.length === 0) {
        return [];
      }

      const metadata = runNxdn(context.workspaceRoot, projectFiles, options || {});
      return metadata.projects.map(project => toNxNode(project, options || {}));
    },
  ],
  _internal: {
    createSelectorTags,
    toNxNode,
  },
};
