const test = require('node:test');
const assert = require('node:assert');
const { isExternalLink } = require('../../src/DmCompanion.Desktop/wwwroot/desktop-links.js');

const app = 'http://127.0.0.1:5190';

test('https link to another origin is external', () => {
    assert.strictEqual(isExternalLink('https://www.dndbeyond.com/srd', app), true);
});

test('http link to another origin is external', () => {
    assert.strictEqual(isExternalLink('http://example.test/a', app), true);
});

test('same origin is internal', () => {
    assert.strictEqual(isExternalLink('http://127.0.0.1:5190/notes/a', app), false);
});

test('relative paths are internal', () => {
    assert.strictEqual(isExternalLink('/_vault/maps/a.png', app), false);
    assert.strictEqual(isExternalLink('notes/a', app), false);
});

test('mailto, javascript and file are not opened', () => {
    assert.strictEqual(isExternalLink('mailto:a@example.test', app), false);
    assert.strictEqual(isExternalLink('javascript:alert(1)', app), false);
    assert.strictEqual(isExternalLink('file:///etc/passwd', app), false);
});

test('empty or missing href is internal', () => {
    assert.strictEqual(isExternalLink('', app), false);
    assert.strictEqual(isExternalLink(null, app), false);
});
