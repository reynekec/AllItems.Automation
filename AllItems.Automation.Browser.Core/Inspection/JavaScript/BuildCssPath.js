function buildCssPath(element) {
  if (!element || element.nodeType !== Node.ELEMENT_NODE) {
    return "";
  }

  if (element.id) {
    return "#" + element.id;
  }

  var segments = [];
  var current = element;

  while (current && current.nodeType === Node.ELEMENT_NODE) {
    var tag = current.tagName.toLowerCase();
    var classNames = (current.className || "")
      .toString()
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(function (value) { return "." + value; })
      .join("");

    var index = 1;
    var sibling = current.previousElementSibling;
    while (sibling) {
      if (sibling.tagName === current.tagName) {
        index++;
      }
      sibling = sibling.previousElementSibling;
    }

    var nth = ":nth-of-type(" + index + ")";
    segments.unshift(tag + classNames + nth);

    if (current.id) {
      segments[0] = "#" + current.id;
      break;
    }

    current = current.parentElement;
  }

  return segments.join(" > ");
}
