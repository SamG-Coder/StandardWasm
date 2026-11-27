const captionStyle = 0x00c00000;
const borderStyle = 0x00800000;
const dialogFrameStyle = 0x00400000;
const thickFrameStyle = 0x00040000;

const modalFrame = 0x00000001;
const toolWindow = 0x00000080;
const clientEdge = 0x00000200;
const staticEdge = 0x00020000;

const invalidParameter = 87;
const noAccess = 998;

// Small DPI values use Windows' reference font-height minimums rather
// than a linear scale. At desktop DPI the baseline metrics scale from 96.
const smallCaptionHeights = [
    0, 24, 24, 24, 6, 6, 6, 6, 6, 6, 6, 6,
    6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7,
    7, 7, 7, 7, 8, 8, 8, 8, 8, 9, 9, 9,
    9, 9, 10, 10, 10, 10, 11, 11, 11, 11, 12, 12
];
const smallMenuHeights = [
    0, 24, 24, 24, 6, 6, 6, 6, 6, 6, 6, 6,
    6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7,
    7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8,
    9, 9, 9, 9, 9, 9, 9, 10, 11, 11, 11, 11,
    11, 11, 11, 11, 13, 13, 13, 13, 13, 13, 13, 13,
    14, 14, 14, 14, 14, 14, 14, 14, 15
];

function referenceMetrics(dpi) {
    const scale = pixels => Math.round(pixels * dpi / 96);
    const caption = smallCaptionHeights[dpi] ?? 1 + scale(22);
    return {
        border: 1, fixedFrame: 3, sizeFrame: 3 + scale(1), clientEdge: 2,
        padding: scale(4), caption, smallCaption: caption,
        menu: smallMenuHeights[dpi] ?? 1 + scale(19)
    };
}

function nonClientInsets(style, extendedStyle, hasMenu, metrics) {
    const caption = (style & captionStyle) === captionStyle;
    const thick = (style & thickFrameStyle) !== 0;
    const border = (style & borderStyle) !== 0;
    const edge = (extendedStyle & staticEdge) !== 0 ? metrics.border : 0;
    let outer;
    if ((extendedStyle & modalFrame) !== 0) outer = metrics.fixedFrame;
    else if ((style & dialogFrameStyle) !== 0 || thick && border) outer = metrics.fixedFrame - edge;
    else if (thick) outer = metrics.fixedFrame - metrics.border - edge;
    else outer = (border ? metrics.border : 0) + edge;

    if (caption || thick) outer += metrics.sizeFrame - metrics.fixedFrame + metrics.padding;
    if ((extendedStyle & clientEdge) !== 0) outer += metrics.clientEdge;

    let top = outer;
    if (caption) top += (extendedStyle & toolWindow) !== 0 ? metrics.smallCaption : metrics.caption;
    if (hasMenu) top += metrics.menu;
    return { outer, top };
}

export function createAdjustWindowRectExForDpi(memory, setError) {
    return function adjustWindowRectExForDpi(pointer, style, menu, extendedStyle, dpi) {
        const address = pointer >>> 0;
        const view = new DataView(memory.buffer);
        if (address === 0 || address > view.byteLength - 16) {
            setError(noAccess);
            return 0;
        }
        dpi >>>= 0;
        if (dpi === 0) {
            setError(invalidParameter);
            return 0;
        }

        // RECT stores independent signed edges, not a width/height pair.
        const { outer, top } = nonClientInsets(style, extendedStyle, menu !== 0, referenceMetrics(dpi));
        view.setInt32(address, view.getInt32(address, true) - outer, true);
        view.setInt32(address + 4, view.getInt32(address + 4, true) - top, true);
        view.setInt32(address + 8, view.getInt32(address + 8, true) + outer, true);
        view.setInt32(address + 12, view.getInt32(address + 12, true) + outer, true);
        return 1;
    };
}
