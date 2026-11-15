const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../src/DmCompanion.Screens/wwwroot/js/audio-player.js'), 'utf8');

// Carga el reproductor real en un contexto con un `window` y un `Audio` falsos.
function load({ failOnPlay = false } = {}) {
    const instances = [];
    const notices = [];
    class FakeAudio {
        constructor() {
            this.paused = true;
            this.playCalls = 0;
            this.listeners = {};
            instances.push(this);
        }
        addEventListener(name, handler) { (this.listeners[name] ??= []).push(handler); }
        removeAttribute() { }
        load() { }
        pause() { this.paused = true; }
        play() {
            this.playCalls++;
            if (failOnPlay) return Promise.reject({ name: 'NotSupportedError' });
            this.paused = false;
            return Promise.resolve();
        }
    }
    const window = {};
    vm.runInNewContext(source, { window, Audio: FakeAudio, setTimeout, clearTimeout });
    const dotnet = { invokeMethodAsync: (method, id) => { notices.push(`${method}:${id}`); return Promise.resolve(); } };
    return { player: window.audioPlayer, instances, notices, dotnet };
}

const flush = () => new Promise(resolve => setImmediate(resolve));

test('un canal nuevo durante la pausa global devuelve ok y no suena', async () => {
    const { player, instances, dotnet } = load();
    player.pauseAll();

    const outcome = await player.play('a', 'u', 1, false, dotnet);

    assert.deepStrictEqual([outcome, instances[0].playCalls], ['ok', 0]);
});

test('un canal nacido en pausa suena al reanudar', async () => {
    const { player, instances, dotnet } = load();
    player.pauseAll();
    await player.play('a', 'u', 1, false, dotnet);
    const pausedBefore = instances[0].paused;

    player.resumeAll();

    assert.deepStrictEqual([pausedBefore, instances[0].paused], [true, false]);
});

test('tras reanudar, un canal nuevo suena al momento', async () => {
    const { player, instances, dotnet } = load();
    player.pauseAll();
    player.resumeAll();

    await player.play('a', 'u', 1, false, dotnet);

    assert.strictEqual(instances[0].paused, false);
});

test('parar todo quita la pausa global', async () => {
    const { player, instances, dotnet } = load();
    player.pauseAll();
    player.stopAll();

    await player.play('a', 'u', 1, false, dotnet);

    assert.strictEqual(instances[0].paused, false);
});

test('un canal nacido en pausa que no arranca al reanudar avisa del fallo', async () => {
    const { player, notices, dotnet } = load({ failOnPlay: true });
    player.pauseAll();
    await player.play('a', 'u', 1, false, dotnet);

    player.resumeAll();
    await flush();

    assert.deepStrictEqual(notices, ['OnFailed:a']);
});

test('pausar justo después de reanudar no da por fallido un canal nacido en pausa', async () => {
    const { player, instances, notices, dotnet } = load();
    player.pauseAll();
    await player.play('a', 'u', 1, false, dotnet);
    instances[0].play = () => Promise.reject({ name: 'AbortError' });

    player.resumeAll();
    player.pauseAll();
    await flush();

    assert.deepStrictEqual(notices, []);
});
