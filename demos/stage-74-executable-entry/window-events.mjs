export function createWindowEventPublisher(windows, listener) {
    return function notifyWinEvent(eventCode, hwnd, objectId, childId) {
        if (typeof listener !== "function") return;
        const owner = windows.getWindowOwner(hwnd);
        if (!owner) return;
        // Deliver before returning to the managed caller. A listener can ask
        // about the window's state during the notification.
        listener({ eventCode: eventCode >>> 0, hwnd: hwnd >>> 0, objectId: objectId | 0, childId: childId | 0,
            threadId: owner.threadId, processId: owner.processId });
    };
}
