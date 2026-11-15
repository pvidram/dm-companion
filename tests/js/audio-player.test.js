const test = require('node:test');
const assert = require('node:assert');
const { clampVolume, classifyPlayFailure, shouldNotifyFailure, markForResume } = require('../../src/DmCompanion.Screens/wwwroot/js/audio-player.js');

test('el volumen dentro de 0-1 no cambia', () => {
    assert.strictEqual(clampVolume(0.4), 0.4);
});

test('el volumen se acota a 0 y a 1', () => {
    assert.deepStrictEqual([clampVolume(-2), clampVolume(7)], [0, 1]);
});

test('un volumen no numérico es silencio', () => {
    assert.deepStrictEqual([clampVolume(NaN), clampVolume(undefined), clampVolume('x')], [0, 0, 0]);
});

test('un play superado por otro no es un fallo', () => {
    assert.strictEqual(classifyPlayFailure('AbortError', false), 'superseded');
});

test('un play que rechaza en el canal vigente devuelve el nombre del error', () => {
    assert.strictEqual(classifyPlayFailure('NotAllowedError', true), 'NotAllowedError');
});

test('un rechazo sin nombre se llama Error', () => {
    assert.strictEqual(classifyPlayFailure(undefined, true), 'Error');
});

test('un error antes del arranque no se notifica', () => {
    assert.strictEqual(shouldNotifyFailure(false, true), false);
});

test('un error tras el arranque en un canal reemplazado no se notifica', () => {
    assert.strictEqual(shouldNotifyFailure(true, false), false);
});

test('un error tras el arranque en el canal vigente se notifica', () => {
    assert.strictEqual(shouldNotifyFailure(true, true), true);
});

test('un canal que sonaba se marca para reanudar', () => {
    assert.strictEqual(markForResume(false, false), true);
});

test('un segundo pauseAll no desmarca un canal ya pausado', () => {
    assert.strictEqual(markForResume(true, true), true);
});

test('un canal que ya estaba pausado de antes no se marca', () => {
    assert.strictEqual(markForResume(false, true), false);
});
