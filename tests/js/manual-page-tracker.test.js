const test = require('node:test');
const assert = require('node:assert');
const { createPageTracker } = require('../../src/DmCompanion.Screens/wwwroot/js/manual-page-tracker.js');

function fakePage(name, log) {
    return { name, cleanup: () => log.push(name) };
}

test('showing another page cleans up the previous one', () => {
    const log = [];
    const tracker = createPageTracker();
    tracker.show(fakePage('p1', log));
    tracker.show(fakePage('p2', log));
    assert.deepStrictEqual(log, ['p1']);
});

test('showing the same page again does not clean it up', () => {
    const log = [];
    const tracker = createPageTracker();
    const page = fakePage('p1', log);
    tracker.show(page);
    tracker.show(page);
    assert.deepStrictEqual(log, []);
});

test('releasing a page that is not visible cleans it up', () => {
    const log = [];
    const tracker = createPageTracker();
    tracker.show(fakePage('visible', log));
    tracker.release(fakePage('searched', log));
    assert.deepStrictEqual(log, ['searched']);
});

test('releasing the visible page keeps it', () => {
    const log = [];
    const tracker = createPageTracker();
    const page = fakePage('visible', log);
    tracker.show(page);
    tracker.release(page);
    assert.deepStrictEqual(log, []);
});
