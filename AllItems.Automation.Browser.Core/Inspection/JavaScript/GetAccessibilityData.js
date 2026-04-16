function getAccessibilityData(element) {
  if (!element || element.nodeType !== Node.ELEMENT_NODE) {
    return {};
  }

  var role = element.getAttribute("role") || "";
  var ariaLabel = element.getAttribute("aria-label") || "";
  var ariaDescription = element.getAttribute("aria-description") || "";
  var ariaLabelledBy = element.getAttribute("aria-labelledby") || "";

  return {
    role: role,
    ariaLabel: ariaLabel,
    ariaDescription: ariaDescription,
    ariaLabelledBy: ariaLabelledBy,
    title: element.getAttribute("title") || "",
    name: element.getAttribute("name") || ""
  };
}
