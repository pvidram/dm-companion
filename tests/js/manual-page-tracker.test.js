const test = require('node:test');
const assert = require('node:assert');
const { createPageTracker } = require('../../src/DmCompanion.Screens/wwwroot/js/manual-page-tracker.js');

function fakePage(name, log) {
    return { name, cleanup: () => log.push(name) };
}

test('removing a tracked page cleans it up', () => {
    const log = [];
    const tracker = createPageTracker();
    const page = fakePage('p1', log);
    tracker.add(page);
    tracker.remove(page);
    assert.deepStrictEqual(log, ['p1']);
});

test('several visible pages are kept until each one leaves', () => {
    const log = [];
    const tracker = createPageTracker();
    const [p1, p2, p3] = ['p1', 'p2', 'p3'].map(name => fakePage(name, log));
    [p1, p2, p3].forEach(page => tracker.add(page));
    tracker.remove(p1);
    tracker.remove(p3);
    assert.deepStrictEqual(log, ['p1', 'p3']);
});

test('removing a page that was never tracked does nothing', () => {
    const log = [];
    const tracker = createPageTracker();
    tracker.remove(fakePage('other', log));
    assert.deepStrictEqual(log, []);
});

test('adding the same page twice and removing it cleans it up once', () => {
    const log = [];
    const tracker = createPageTracker();
    const page = fakePage('p1', log);
    tracker.add(page);
    tracker.add(page);
    tracker.remove(page);
    tracker.remove(page);
    assert.deepStrictEqual(log, ['p1']);
});

test('releasing a page that is not visible cleans it up', () => {
    const log = [];
    const tracker = createPageTracker();
    tracker.add(fakePage('visible', log));
    tracker.release(fakePage('searched', log));
    assert.deepStrictEqual(log, ['searched']);
});

test('releasing a visible page keeps it', () => {
    const log = [];
    const tracker = createPageTracker();
    const page = fakePage('visible', log);
    tracker.add(page);
    tracker.release(page);
    assert.deepStrictEqual(log, []);
});
