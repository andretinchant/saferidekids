// Bridge JS para download de arquivos gerados server-side.
// Recebe base64 + MIME, materializa Blob e dispara download.
window.srkDownloadFile = function (filename, base64Data, mimeType) {
    try {
        const byteString = atob(base64Data);
        const buffer = new Uint8Array(byteString.length);
        for (let i = 0; i < byteString.length; i++) {
            buffer[i] = byteString.charCodeAt(i);
        }
        const blob = new Blob([buffer], { type: mimeType || 'application/octet-stream' });

        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename || 'download.bin';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        // Libera memória após pequena espera para permitir download em alguns browsers.
        setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
    } catch (e) {
        console.error('srkDownloadFile error:', e);
    }
};
