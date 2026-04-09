function inspectShadowDom(rootElement) {
  function inspectNode(node) {
    if (!node || node.nodeType !== Node.ELEMENT_NODE) {
      return null;
    }

    var isShadowHost = !!node.shadowRoot;
    var children = [];

    var directChildren = Array.from(node.children || []);
    for (var i = 0; i < directChildren.length; i++) {
      var childNode = inspectNode(directChildren[i]);
      if (childNode) {
        children.push(childNode);
      }
    }

    var shadowChildren = [];
    if (node.shadowRoot) {
      var shadowNodes = Array.from(node.shadowRoot.children || []);
      for (var j = 0; j < shadowNodes.length; j++) {
        var shadowNode = inspectNode(shadowNodes[j]);
        if (shadowNode) {
          shadowChildren.push(shadowNode);
        }
      }
    }

    return {
      tagName: node.tagName.toLowerCase(),
      id: node.id || "",
      isShadowHost: isShadowHost,
      children: children,
      shadowChildren: shadowChildren
    };
  }

  if (!rootElement || rootElement.nodeType !== Node.ELEMENT_NODE) {
    return null;
  }

  return inspectNode(rootElement);
}
