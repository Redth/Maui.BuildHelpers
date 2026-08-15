const assert = require('node:assert/strict');
const test = require('node:test');
const { _internal } = require('./index');

function createProject() {
  return {
    projectFile: 'src/App/App.csproj',
    projectRoot: 'src/App',
    projectType: 'application',
    technologies: ['dotnet', 'C#'],
    capabilities: {
      isTest: true,
      isExecutable: true,
      isPackable: false,
      isPublishable: true,
      isTool: false,
      packageIds: [],
    },
    configurations: [
      {
        framework: {
          shortName: 'net10.0-ios',
          platform: 'ios',
        },
        runtimeIdentifiers: ['ios-arm64'],
      },
    ],
    targetHostRequirements: [
      {
        target: 'build',
        hosts: ['macos'],
        source: 'inferred',
      },
      {
        target: 'test',
        hosts: ['macos'],
        source: 'explicit',
      },
    ],
    explicitTags: ['scope:client'],
    diagnostics: [],
  };
}

test('selector tags are opt-in and namespaced', () => {
  const project = createProject();

  assert.deepEqual(_internal.createSelectorTags(project, {}), []);
  assert.deepEqual(
    _internal.createSelectorTags(project, {
      selectorTags: ['target-framework', 'platform', 'runtime-identifier'],
    }),
    [
      'dotnet:platform:ios',
      'dotnet:rid:ios-arm64',
      'dotnet:tfm:net10.0-ios',
    ]);
});

test('host selectors require explicit metadata unless inference is enabled', () => {
  const project = createProject();

  assert.deepEqual(
    _internal.createSelectorTags(project, { selectorTags: ['host'] }),
    []);
  assert.deepEqual(
    _internal.createSelectorTags(project, {
      selectorTags: ['host'],
      includeInferredHostSelectors: true,
    }),
    ['dotnet:host:build:macos']);
  assert.deepEqual(
    _internal.createSelectorTags(project, {
      selectorTags: ['host'],
      hostTarget: 'test',
    }),
    ['dotnet:host:test:macos']);
});

test('Nx node keeps explicit tags separate from selector provenance', () => {
  const [, result] = _internal.toNxNode(createProject(), {
    selectorTags: ['platform'],
  });
  const node = result.projects['src/App'];

  assert.equal(node.projectType, 'application');
  assert.deepEqual(node.tags, ['dotnet:platform:ios', 'scope:client']);
  assert.deepEqual(node.metadata.technologies, ['dotnet', 'C#']);
  assert.deepEqual(node.metadata.dotnetNx.explicitTags, ['scope:client']);
  assert.deepEqual(node.metadata.dotnetNx.selectorTags, ['dotnet:platform:ios']);
});
