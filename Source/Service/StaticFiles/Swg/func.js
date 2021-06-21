document.addEventListener("input", async (e) => {
    const target = e.target;

    if (!target) {
        return;
    }

    if (target.type === "file") {
        if (!target.files || !target.files.length) {
            return;
        }

        if (target.id === "base64Input") {
            const toast = Toastify({
                text: "Copied " + target.files[0].name + " as Base64 to clipboard.",
                duration: 3000
            });
            const base64 = await ReadFromInput(target);
            copyToClipboard(base64);
            toast.showToast();
        }
    }
});

const ReadFromInput = async (input) => {
    if (!input || !input.files || !input.files.length) {
        return "";
    }

    const file = input.files[0];
    const fileBuffer = await _readInputFileBuffer(file);
    return _arrayBufferToBase64(fileBuffer);
}

const _readInputFileBuffer = async (file) => {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.readAsArrayBuffer(file);
        reader.onload = () => {
            if (typeof reader.result === "string") {
                resolve(Buffer.from(reader.result, 0, reader.result.length));
            }
            else {
                resolve(reader.result);
            }
        };
        reader.onerror = error => {
            reject(error);
        };
    });
};

const _arrayBufferToBase64 = (buffer) => {
    const bytes = new Uint8Array(buffer);
    const len = bytes.byteLength;

    let binary = "";
    for (let i = 0; i < len; i++) {
        binary += String.fromCharCode(bytes[i]);
    }

    return window.btoa(binary);
};

function copyToClipboard(text) {
    const temp = document.createElement("textarea");
    document.body.appendChild(temp);
    temp.value = text;
    temp.select();
    document.execCommand("copy");
    document.body.removeChild(temp);
}