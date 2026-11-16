const test = require('node:test');
const assert = require('node:assert');
const { loadDocument, renderWithRetry } = require('../../src/DmCompanion.Screens/wwwroot/js/manual-pdf-tasks.js');

function cancelled() {
    const error = new Error('cancelled');
    error.name = 'RenderingCancelledException';
    return error;
}

test('a failed load destroys the task before rethrowing', async () => {
    const log = [];
    const task = { promise: Promise.reject(new Error('InvalidPDFException')), destroy: async () => log.push('destroy') };
    await assert.rejects(loadDocument(task));
    assert.deepStrictEqual(log, ['destroy']);
});

test('a failed load still rethrows when destroy fails', async () => {
    const task = { promise: Promise.reject(new Error('InvalidPDFException')), destroy: () => Promise.reject(new Error('x')) };
    await assert.rejects(loadDocument(task), /InvalidPDFException/);
});

test('a successful load returns the document', async () => {
    const task = { promise: Promise.resolve('doc'), destroy: async () => assert.fail('destroyed') };
    assert.strictEqual(await loadDocument(task), 'doc');
});

test('a render that fails is retried once at half scale', async () => {
    const factors = [];
    const painted = await renderWithRetry(async factor => {
        factors.push(factor);
        if (factor === 1) throw new Error('OutOfMemory');
    });
    assert.deepStrictEqual({ painted, factors }, { painted: true, factors: [1, 0.5] });
});

test('a render that fails twice reports false', async () => {
    const painted = await renderWithRetry(async () => { throw new Error('OutOfMemory'); });
    assert.strictEqual(painted, false);
});

test('a cancelled render is not retried and is not a failure', async () => {
    const factors = [];
    const painted = await renderWithRetry(async factor => { factors.push(factor); throw cancelled(); });
    assert.deepStrictEqual({ painted, factors }, { painted: true, factors: [1] });
});

test('a retry that is cancelled is not a failure', async () => {
    const painted = await renderWithRetry(async factor => { throw factor === 1 ? new Error('OutOfMemory') : cancelled(); });
    assert.strictEqual(painted, true);
});
