chrome.runtime.onInstalled.addListener(() => {
    chrome.contextMenus.create({
        id: "copyToNotes",
        title: "Copy to Notes",
        contexts: ["selection"]
    });
});

chrome.contextMenus.onClicked.addListener((info) => {
    if (info.menuItemId === "copyToNotes" && info.selectionText) {
        fetch("http://localhost:8080/", {
            method: "POST",
            headers: {
                "Content-Type": "text/plain"
            },
            body: info.selectionText
        });
    }
});