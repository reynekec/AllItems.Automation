function inspectPage() {
  function summarizeElement(element) {
    if (!element || element.nodeType !== Node.ELEMENT_NODE) {
      return null;
    }

    return {
      tagName: element.tagName.toLowerCase(),
      id: element.id || "",
      name: element.getAttribute("name") || "",
      text: (element.textContent || "").trim().slice(0, 200)
    };
  }

  var root = summarizeElement(document.documentElement);
  var frameElements = Array.from(document.querySelectorAll("iframe"));

  var frames = frameElements.map(function (frame, index) {
    var frameUrl = frame.getAttribute("src") || "";
    var frameName = frame.getAttribute("name") || "frame-" + (index + 1);
    var rootElements = [];

    try {
      var doc = frame.contentDocument;
      if (doc && doc.documentElement) {
        rootElements.push(summarizeElement(doc.documentElement));
      }
    } catch (_error) {
      rootElements.push({
        tagName: "iframe",
        id: frame.id || "",
        name: frameName,
        text: "cross-origin inaccessible"
      });
    }

    return {
      name: frameName,
      url: frameUrl,
      rootElements: rootElements.filter(Boolean)
    };
  });

  return {
    pageUrl: window.location.href,
    root: root,
    frames: frames
  };
}
