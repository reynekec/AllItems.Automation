function buildXPath(element) {
  if (!element || element.nodeType !== Node.ELEMENT_NODE) {
    return "";
  }

  if (element.id) {
    return "//*[@id='" + element.id + "']";
  }

  var segments = [];
  var current = element;

  while (current && current.nodeType === Node.ELEMENT_NODE) {
    var index = 1;
    var sibling = current.previousElementSibling;
    while (sibling) {
      if (sibling.tagName === current.tagName) {
        index++;
      }
      sibling = sibling.previousElementSibling;
    }

    segments.unshift(current.tagName.toLowerCase() + "[" + index + "]");
    current = current.parentElement;
  }

  return "/" + segments.join("/");
}
