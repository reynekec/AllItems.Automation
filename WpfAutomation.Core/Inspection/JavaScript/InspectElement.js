function inspectElement(target) {
  if (!target || target.nodeType !== Node.ELEMENT_NODE) {
    return null;
  }

  function toAttributes(element) {
    var result = {};
    var attributes = element.attributes || [];
    for (var i = 0; i < attributes.length; i++) {
      result[attributes[i].name] = attributes[i].value;
    }
    return result;
  }

  function cssPath(element) {
    if (element.id) {
      return "#" + element.id;
    }

    var parts = [];
    var current = element;
    while (current && current.nodeType === Node.ELEMENT_NODE) {
      var index = 1;
      var previous = current.previousElementSibling;
      while (previous) {
        if (previous.tagName === current.tagName) {
          index++;
        }
        previous = previous.previousElementSibling;
      }

      parts.unshift(current.tagName.toLowerCase() + ":nth-of-type(" + index + ")");
      if (current.id) {
        parts[0] = "#" + current.id;
        break;
      }

      current = current.parentElement;
    }

    return parts.join(" > ");
  }

  function xPath(element) {
    if (element.id) {
      return "//*[@id='" + element.id + "']";
    }

    var parts = [];
    var current = element;
    while (current && current.nodeType === Node.ELEMENT_NODE) {
      var index = 1;
      var previous = current.previousElementSibling;
      while (previous) {
        if (previous.tagName === current.tagName) {
          index++;
        }
        previous = previous.previousElementSibling;
      }

      parts.unshift(current.tagName.toLowerCase() + "[" + index + "]");
      current = current.parentElement;
    }

    return "/" + parts.join("/");
  }

  function computedStyles(element) {
    var styles = window.getComputedStyle(element);
    return {
      display: styles.display,
      visibility: styles.visibility,
      color: styles.color,
      backgroundColor: styles.backgroundColor,
      fontSize: styles.fontSize,
      width: styles.width,
      height: styles.height
    };
  }

  function inspectNode(element, depth) {
    if (!element || element.nodeType !== Node.ELEMENT_NODE) {
      return null;
    }

    var rect = element.getBoundingClientRect();
    var children = [];
    if (depth < 10) {
      var domChildren = Array.from(element.children || []);
      for (var i = 0; i < domChildren.length; i++) {
        var child = inspectNode(domChildren[i], depth + 1);
        if (child) {
          children.push(child);
        }
      }
    }

    return {
      tagName: element.tagName.toLowerCase(),
      id: element.id || "",
      name: element.getAttribute("name") || "",
      text: (element.textContent || "").trim(),
      innerText: (element.innerText || "").trim(),
      classes: Array.from(element.classList || []),
      attributes: toAttributes(element),
      styles: computedStyles(element),
      boundingBox: {
        x: rect.x,
        y: rect.y,
        width: rect.width,
        height: rect.height,
        offsetWidth: element.offsetWidth,
        offsetHeight: element.offsetHeight
      },
      cssPath: cssPath(element),
      xPath: xPath(element),
      isShadowHost: !!element.shadowRoot,
      isInShadowDom: !!element.getRootNode && element.getRootNode() instanceof ShadowRoot,
      children: children
    };
  }

  return inspectNode(target, 0);
}
