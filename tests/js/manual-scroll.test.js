const test = require('node:test');
const assert = require('node:assert');
const {
    layout, slotHeight, currentPage, paintWindow, compensateScroll, anchorAt, scrollForAnchor, pixelBudget,
    previewOrigin, horizontalFraction, scrollForHorizontal, fillUnknownSizes, remapScroll
} = require('../../src/DmCompanion.Screens/wwwroot/js/manual-scroll.js');

// Cuatro huecos de 100 px con 10 px entre ellos: tops 0, 110, 220, 330.
const HEIGHTS = [100, 100, 100, 100];
const TOPS = [0, 110, 220, 330];

test('layout stacks the slots with the gap', () => {
    assert.deepStrictEqual(layout(HEIGHTS, 10), { tops: TOPS, total: 430 });
});

test('layout of no pages is empty', () => {
    assert.deepStrictEqual(layout([], 10), { tops: [], total: 0 });
});

test('slotHeight keeps the aspect ratio', () => {
    assert.strictEqual(slotHeight(600, 800, 300), 400);
});

test('slotHeight falls back to a square for an invalid size', () => {
    assert.strictEqual(slotHeight(0, 800, 300), 300);
});

test('currentPage is the one with most visible height', () => {
    assert.strictEqual(currentPage(TOPS, HEIGHTS, 80, 100), 2);
});

test('currentPage at the top is the first page', () => {
    assert.strictEqual(currentPage(TOPS, HEIGHTS, 0, 150), 1);
});

test('currentPage tie goes to the lowest page', () => {
    assert.strictEqual(currentPage([0, 100], [100, 100], 50, 100), 1);
});

test('currentPage with the view in a gap picks the nearest page', () => {
    assert.strictEqual(currentPage(TOPS, HEIGHTS, 100, 5), 1);
});

test('currentPage without pages is 1', () => {
    assert.strictEqual(currentPage([], [], 0, 500), 1);
});

test('paintWindow takes the pages touching the view plus one screen of margin', () => {
    assert.deepStrictEqual(paintWindow(TOPS, HEIGHTS, 0, 100, 100, 1), { first: 1, last: 2 });
});

test('paintWindow always includes the neighbours of the current page', () => {
    assert.deepStrictEqual(paintWindow(TOPS, HEIGHTS, 230, 100, 0, 3), { first: 2, last: 4 });
});

test('paintWindow is clamped to the document', () => {
    assert.deepStrictEqual(paintWindow(TOPS, HEIGHTS, 330, 100, 0, 4), { first: 3, last: 4 });
});

test('paintWindow with a huge margin covers every page', () => {
    assert.deepStrictEqual(paintWindow(TOPS, HEIGHTS, 0, 100, 1000, 1), { first: 1, last: 4 });
});

test('paintWindow without pages is empty', () => {
    assert.deepStrictEqual(paintWindow([], [], 0, 100, 100, 1), { first: 1, last: 0 });
});

test('compensateScroll shifts the view when a slot above it grows', () => {
    assert.strictEqual(compensateScroll(300, 0, 100, 150), 350);
});

test('compensateScroll treats a slot ending exactly at the view top as above', () => {
    assert.strictEqual(compensateScroll(100, 0, 100, 60), 60);
});

test('compensateScroll keeps the proportion inside the slot that straddles the view top', () => {
    assert.strictEqual(compensateScroll(250, 200, 100, 200), 300);
});

test('compensateScroll ignores a slot below the view', () => {
    assert.strictEqual(compensateScroll(300, 400, 100, 250), 300);
});

test('zoom keeps the page and the fraction at the top of the view', () => {
    const anchor = anchorAt(TOPS, HEIGHTS, 135, 2);
    const doubled = [200, 200, 200, 200];
    assert.deepStrictEqual(
        [anchor.page, anchor.fraction, scrollForAnchor(layout(doubled, 10).tops, doubled, anchor)],
        [2, 0.25, 260]);
});

test('zoom keeps a negative fraction when the page starts below the view top', () => {
    const anchor = anchorAt(TOPS, HEIGHTS, 100, 2);
    const doubled = [200, 200, 200, 200];
    assert.strictEqual(scrollForAnchor(layout(doubled, 10).tops, doubled, anchor), 190);
});

test('pixel budget splits the total among the painted pages', () => {
    assert.strictEqual(pixelBudget(1000, 4), 250);
});

test('pixel budget of one page or none is the whole total', () => {
    assert.deepStrictEqual([pixelBudget(1000, 1), pixelBudget(1000, 0)], [1000, 1000]);
});

test('previewOrigin puts the origin at the top left of the view in column coordinates', () => {
    assert.strictEqual(previewOrigin(30, 4000, 10, 0), '20px 4000px');
});

test('horizontal anchor keeps the fraction of the column at the left of the view', () => {
    const fraction = horizontalFraction(150, 0, 600);
    assert.deepStrictEqual([fraction, scrollForHorizontal(0, 1200, fraction)], [0.25, 300]);
});

test('horizontal anchor accounts for the margin of a centered column', () => {
    assert.strictEqual(scrollForHorizontal(20, 600, horizontalFraction(70, 40, 400)), 65);
});

test('horizontal scroll never goes below zero', () => {
    assert.strictEqual(scrollForHorizontal(0, 600, -0.2), 0);
});

test('fillUnknownSizes gives the last size read to the pages not read yet', () => {
    const sizes = [{ width: 1, height: 1 }, { width: 1, height: 1 }, { width: 1, height: 1 }];
    const read = { width: 5, height: 7 };
    assert.deepStrictEqual(fillUnknownSizes(sizes, [true, false, true], read),
        [sizes[0], read, sizes[2]]);
});

test('remapScroll shifts by the change of the slots above the view', () => {
    const newHeights = [150, 100, 100, 100];
    assert.strictEqual(remapScroll(TOPS, HEIGHTS, layout(newHeights, 10).tops, newHeights, 250), 300);
});

test('remapScroll keeps the proportion inside the slot at the view top', () => {
    const newHeights = [100, 100, 200, 100];
    assert.strictEqual(remapScroll(TOPS, HEIGHTS, layout(newHeights, 10).tops, newHeights, 270), 320);
});

test('remapScroll adds all the changes above the view', () => {
    const newHeights = [150, 130, 100, 100];
    assert.strictEqual(remapScroll(TOPS, HEIGHTS, layout(newHeights, 10).tops, newHeights, 330), 410);
});

test('remapScroll leaves a view above the changes where it was', () => {
    const newHeights = [100, 100, 100, 300];
    assert.strictEqual(remapScroll(TOPS, HEIGHTS, layout(newHeights, 10).tops, newHeights, 50), 50);
});
