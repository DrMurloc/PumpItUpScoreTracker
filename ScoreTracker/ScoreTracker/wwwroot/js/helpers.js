export async function refreshLogin() {
    await fetch("/Logout/Refresh");
}


export async function downloadFileFromStream(fileName, contentStreamReference) {
    console.log(contentStreamReference);
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    console.log(arrayBuffer);
    const blob = new Blob([arrayBuffer]);
    console.log(blob);
        const url = URL.createObjectURL(blob);
    
        triggerFileDownload(fileName, url);

        URL.revokeObjectURL(url);
    }

export function triggerFileDownload(fileName, url) {
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
}