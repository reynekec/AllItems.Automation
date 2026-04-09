function getComputedStylesSubset(element) {
  if (!element || element.nodeType !== Node.ELEMENT_NODE) {
    return {};
  }

  var styles = window.getComputedStyle(element);
  return {
    display: styles.display,
    visibility: styles.visibility,
    opacity: styles.opacity,
    position: styles.position,
    color: styles.color,
    backgroundColor: styles.backgroundColor,
    fontSize: styles.fontSize,
    fontWeight: styles.fontWeight,
    width: styles.width,
    height: styles.height
  };
}
