const MuffinComms = {
    _callbacks: [],
    _dispatchCallback: function (id, message) {
        MuffinComms._callbacks[id](message);
    },
    isAvailable: function () {
        return (window.androidMuffinComms && window.androidMuffinComms._dispatch) || (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.muffinComms);
    },
    verifyAvailable: function () {
        if (!MuffinComms.isAvailable()) {
            throw "[MuffinComms] Could not complete the requested operation as the webkit service is not available";
        }
    },
    //thanks https://stackoverflow.com/questions/30106476/using-javascripts-atob-to-decode-base64-doesnt-properly-decode-utf-8-strings
    _encode: function(str) {
        return btoa(encodeURIComponent(str).replace(/%([0-9A-F]{2})/g, (match, g1) => String.fromCharCode('0x' + g1)));
    },
    _decode: function(str) {
        return decodeURIComponent(atob(str).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
    },
    send: function (message, data, responseType = "text") {
        MuffinComms.verifyAvailable();
        if (message === undefined) {
            throw "[MuffinComms] Message is undefined!";
        }
        if (data === undefined) {
            data = null;
        }
        return new Promise(function (resolve) {
            let id = MuffinComms._callbacks.push(function (data) {
                if (data === null) resolve(data);

                if (responseType == "text") {
                    resolve(data);
                } else if (responseType == "json") {
                    resolve(JSON.parse(data));
                } else if (responseType == "arraybuffer") {
                    const length = data.length;
                    const buffer = new ArrayBuffer(length);
                    const view = new Uint8Array(buffer);

                    for (let i = 0; i < length; i++) {
                        view[i] = data.charCodeAt(i);
                    }

                    resolve(buffer);
                } else if (responseType == "none") {
                    resolve();
                } else {
                    console.error(`[MuffinComms] Unsupported responseType '${responseType}'`);
                }
            }) - 1;
            if (window.webkit) {
                window.webkit.messageHandlers.muffinComms.postMessage(JSON.stringify({ message: message, data: data, id: id }));
            } else if (window.androidMuffinComms) {
                window.androidMuffinComms._dispatch(JSON.stringify({ message: message, data: data, id: id }));
            }
        });
    },
    serialize: function(object) {
        if (object instanceof ArrayBuffer) {
            var binary = '';
            var bytes = new Uint8Array(object);
            var len = bytes.byteLength;
            for (var i = 0; i < len; i++) {
                binary += String.fromCharCode( bytes[ i ] );
            }
            return btoa(binary); //this is fine as it is binary
        } else {
            throw "No serializer for " + typeof object;
        }
    },
    deserialize: function(encoded, to) {
        let binaryData = atob(encoded) //fine again - binary;
        if (to === Uint8Array) {
            const uint8Array = new Uint8Array(binaryData.length);
            for (let i = 0; i < binaryData.length; i++) {
                uint8Array[i] = binaryData.charCodeAt(i);
            }
            return uint8Array;
        } else {
            throw "No deserializer for " + to;
        }
    }
};

MuffinComms._webkitMessage = MuffinComms._dispatchCallback;
