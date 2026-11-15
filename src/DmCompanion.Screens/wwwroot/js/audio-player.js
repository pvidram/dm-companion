// Reproductor de ambiente sonoro del navegador (DevHost y PC): un HTMLAudioElement por canal. window.audioPlayer.
// Solo carga y reproduce; fundidos, listas y escenas viven en C# (AudioMixer). La App Android no carga este script.
(function (root) {
    'use strict';

    // Volumen acotado a 0-1; lo no numérico cuenta como silencio.
    function clampVolume(value) {
        const n = Number(value);
        if (!Number.isFinite(n)) return 0;
        return Math.max(0, Math.min(1, n));
    }

    // Resultado de un play() que rechaza: un canal ya reemplazado o parado no es un fallo; si no, el nombre del error.
    function classifyPlayFailure(errorName, isCurrent) {
        return isCurrent ? (errorName || 'Error') : 'superseded';
    }

    // OnFailed solo para errores posteriores a un arranque correcto y de un canal que sigue siendo el vigente;
    // antes del arranque el único camino de fallo es el resultado de play().
    function shouldNotifyFailure(started, isCurrent) {
        return started && isCurrent;
    }

    // Un canal se reanuda si ya estaba marcado (pauseAll repetido) o si sonaba al pausar.
    function markForResume(alreadyMarked, audioPaused) {
        return alreadyMarked || !audioPaused;
    }

    const api = { clampVolume, classifyPlayFailure, shouldNotifyFailure, markForResume };
    root.audioPlayerMath = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (typeof window === 'undefined') return;

    const START_TIMEOUT_MS = 10000;
    const channels = new Map(); // id -> { audio, dotnet, resumeOnResume, started }
    // Pausa global (contrato de IAudioPlayer): un canal que nace en pausa espera a resumeAll para sonar. Así tampoco suena
    // la pista siguiente que pide un 'ended' llegado justo después de pauseAll.
    let globalPaused = false;

    function release(id) {
        const channel = channels.get(id);
        if (!channel) return;
        channels.delete(id);
        channel.audio.pause();
        channel.audio.removeAttribute('src');
        channel.audio.load();
    }

    function notify(channel, method, id) {
        if (channel.dotnet) channel.dotnet.invokeMethodAsync(method, id).catch(() => { /* circuito cerrado */ });
    }

    // Empieza (o reemplaza) el canal. Devuelve 'ok', 'superseded' (otro play o stop lo adelantó: no es un fallo)
    // o el nombre del error (p. ej. NotAllowedError por falta de gesto, TimeoutError).
    async function play(id, url, volume, loop, dotnet) {
        release(id);
        const audio = new Audio();
        audio.preload = 'auto';
        audio.loop = !!loop;
        audio.volume = clampVolume(volume);
        const channel = { audio, dotnet, resumeOnResume: false, started: false };
        channels.set(id, channel);
        audio.addEventListener('ended', () => {
            if (channels.get(id) !== channel) return;
            channels.delete(id);
            notify(channel, 'OnEnded', id);
        });
        audio.addEventListener('error', () => {
            if (!shouldNotifyFailure(channel.started, channels.get(id) === channel)) return;
            channels.delete(id);
            notify(channel, 'OnFailed', id);
        });
        audio.src = url;
        if (globalPaused) {
            channel.resumeOnResume = true;
            return 'ok';
        }
        let timer;
        const timeout = new Promise((_, reject) => {
            timer = setTimeout(() => reject({ name: 'TimeoutError' }), START_TIMEOUT_MS);
        });
        try {
            await Promise.race([audio.play(), timeout]);
            channel.started = true;
            return channels.get(id) === channel ? 'ok' : 'superseded';
        } catch (e) {
            const outcome = classifyPlayFailure(e && e.name, channels.get(id) === channel);
            if (outcome !== 'superseded') release(id);
            return outcome;
        } finally {
            clearTimeout(timer);
        }
    }

    function setVolume(id, volume) {
        const channel = channels.get(id);
        if (channel) channel.audio.volume = clampVolume(volume);
    }

    function stop(id) { release(id); }

    function pauseAll() {
        globalPaused = true;
        for (const channel of channels.values()) {
            channel.resumeOnResume = markForResume(channel.resumeOnResume, channel.audio.paused);
            channel.audio.pause();
        }
    }

    function resumeAll() {
        globalPaused = false;
        for (const [id, channel] of channels) {
            if (!channel.resumeOnResume) continue;
            channel.resumeOnResume = false;
            resumeChannel(id, channel);
        }
    }

    // Un canal que ya arrancó y no vuelve es un bloqueo del navegador (se ignora); uno nacido en pausa que no arranca
    // es un fichero que no se reproduce, y como su play() ya devolvió 'ok' el fallo se avisa con OnFailed.
    function resumeChannel(id, channel) {
        channel.audio.play().then(() => {
            channel.started = true;
        }).catch((error) => {
            if (channel.started || channels.get(id) !== channel) return;
            // Una pausa global antes de que arranque aborta play(): el canal ya quedó marcado y se reintenta al reanudar.
            if (globalPaused || (error && error.name === 'AbortError')) return;
            release(id);
            notify(channel, 'OnFailed', id);
        });
    }

    function stopAll() {
        globalPaused = false;
        for (const id of Array.from(channels.keys())) release(id);
    }

    window.audioPlayer = { play, setVolume, stop, pauseAll, resumeAll, stopAll };
})(typeof window !== 'undefined' ? window : globalThis);
