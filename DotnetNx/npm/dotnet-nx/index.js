// @ts-check

const childProcess = require('child_process');
const path = require('path');

const PROJECT_PATTERN = '**/*.{csproj,fsproj,vbproj}';

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

function toNxNode(project) {
  const projectRoot = project.projectRoot && project.projectRoot !== '.'
    ? project.projectRoot
    : path.dirname(project.projectFile);

  return [
    project.projectFile,
    {
      projects: {
        [projectRoot]: {
          tags: project.tags,
          metadata: {
            dotnetNx: {
              buildableOn: project.buildableOn,
              resolution: project.resolution,
              sourceFile: project.sourceFile,
              targetFrameworks: project.targetFrameworks,
              diagnostics: project.diagnostics,
            },
          },
        },
      },
    },
  ];
}

module.exports = {
  createNodesV2: [
    PROJECT_PATTERN,
    async (projectFiles, options, context) => {
      if (!projectFiles || projectFiles.length === 0) {
        return [];
      }

      const metadata = runNxdn(context.workspaceRoot, projectFiles, options || {});
      return metadata.projects.map(toNxNode);
    },
  ],
};
