// Songs on the Read tab: record a few seconds from the microphone and ask AudD (audd.io) what's playing.
// The AudD key comes from Settings and is only ever sent to api.audd.io.
window.palabrasSongs = (function () {
    let recorder = null;
    let stream = null;
    let stopTimer = null;
    let cancelled = false;
    let starting = false;
    const clips = {};
    let nextId = 1;

    function supported() {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia && window.MediaRecorder);
    }

    // The first format this browser can record (Safari records mp4, the others webm or ogg).
    function pickType() {
        const types = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4', 'audio/ogg;codecs=opus', 'audio/ogg'];
        for (let i = 0; i < types.length; i++) {
            try { if (MediaRecorder.isTypeSupported(types[i])) return types[i]; } catch (e) { }
        }
        return '';
    }

    function releaseMic() {
        if (stream) {
            stream.getTracks().forEach(function (t) { t.stop(); });
            stream = null;
        }
    }

    return {
        supported: supported,

        // Records for ms milliseconds. Resolves { ok, id } (the clip waits here for send) or { ok: false, error }:
        // unsupported, denied (microphone blocked), nomic, failed, cancelled, empty.
        // dotnet (optional): told when recording really starts (after any permission prompt), via RecordingStarted.
        record: async function (ms, dotnet) {
            if (!supported()) return { ok: false, error: 'unsupported' };
            if (recorder || starting) return { ok: false, error: 'busy' };
            cancelled = false;
            starting = true;
            try {
                // Music, not a voice call: no echo cancelling, noise suppression or volume levelling.
                stream = await navigator.mediaDevices.getUserMedia({
                    audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false }
                });
            } catch (e) {
                starting = false;
                const name = e && e.name;
                return { ok: false, error: name === 'NotAllowedError' || name === 'SecurityError' ? 'denied' : name === 'NotFoundError' ? 'nomic' : 'failed' };
            }
            starting = false;
            if (cancelled) { releaseMic(); return { ok: false, error: 'cancelled' }; }

            const type = pickType();
            const chunks = [];
            try {
                recorder = type ? new MediaRecorder(stream, { mimeType: type }) : new MediaRecorder(stream);
            } catch (e) {
                releaseMic();
                return { ok: false, error: 'failed' };
            }

            return await new Promise(function (resolve) {
                const r = recorder;
                r.ondataavailable = function (e) { if (e.data && e.data.size) chunks.push(e.data); };
                r.onerror = function () { clearTimeout(stopTimer); releaseMic(); recorder = null; resolve({ ok: false, error: 'failed' }); };
                r.onstop = function () {
                    clearTimeout(stopTimer);
                    releaseMic();
                    recorder = null;
                    if (cancelled) { resolve({ ok: false, error: 'cancelled' }); return; }
                    const blob = new Blob(chunks, { type: r.mimeType || type || 'audio/webm' });
                    if (blob.size === 0) { resolve({ ok: false, error: 'empty' }); return; }
                    const id = String(nextId++);
                    clips[id] = blob;
                    resolve({ ok: true, id: id });
                };
                r.start(1000);
                stopTimer = setTimeout(function () { if (r.state !== 'inactive') r.stop(); }, ms);
                if (dotnet) { try { dotnet.invokeMethodAsync('RecordingStarted').catch(function () { }); } catch (e) { } }
            });
        },

        // Stops listening early; the recording is thrown away.
        cancel: function () {
            cancelled = true;
            if (recorder && recorder.state !== 'inactive') recorder.stop();
            else releaseMic();
        },

        // Sends a recorded clip to AudD. Resolves { ok: true, status, body } (AudD's JSON as text) or { ok: false, error }.
        send: async function (id, token) {
            const blob = clips[id];
            delete clips[id];
            if (!blob) return { ok: false, error: 'missing' };
            const ext = blob.type.indexOf('mp4') >= 0 ? 'm4a' : blob.type.indexOf('ogg') >= 0 ? 'ogg' : 'webm';
            const form = new FormData();
            form.append('api_token', token);
            form.append('file', blob, 'clip.' + ext);
            try {
                const res = await fetch('https://api.audd.io/', { method: 'POST', body: form });
                return { ok: true, status: res.status, body: await res.text() };
            } catch (e) {
                return { ok: false, error: 'network' };
            }
        }
    };
})();
