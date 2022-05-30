using System;
using System.Collections.Generic;
using System.Linq;
using MGroup.MSolve.Discretization;
using MGroup.MSolve.Discretization.Embedding;
using MGroup.MSolve.Discretization.Entities;

//TODO: this and EmbeddedGrouping have most things in common. Use a base class for them and template method or use polymorhism from the composed classes.
namespace MGroup.FEM.Structural.Embedding
{
	/// <summary>
	/// Appropriate for iplementing embedding kinematic constraints only for some nodes of the embedded element so that bond slip phenomena can be modeled.
	/// Authors: Gerasimos Sotiropoulos
	/// </summary>
	public class EmbeddedCohesiveGrouping
	{
		private readonly IModel model;
		private readonly IEnumerable<IElementType> hostGroup;
		private readonly IEnumerable<IElementType> embeddedGroup;
		private readonly bool hasEmbeddedRotations = false;

		public IEnumerable<IElementType> HostGroup => hostGroup;
		public IEnumerable<IElementType> EmbeddedGroup => embeddedGroup;

		public EmbeddedCohesiveGrouping(IModel model, IEnumerable<IElementType> hostGroup,
			IEnumerable<IElementType> embeddedGroup, bool hasEmbeddedRotations = false)
		{
			this.model = model;
			this.hostGroup = hostGroup;
			this.embeddedGroup = embeddedGroup;
			this.hasEmbeddedRotations = hasEmbeddedRotations;
			hostGroup.Select(e => e).Distinct().ToList().ForEach(et =>
			{
				if (!(et is IEmbeddedHostElement))
					throw new ArgumentException("EmbeddedGrouping: One or more elements of host group does NOT implement IEmbeddedHostElement.");
			});
			embeddedGroup.Select(e => e).Distinct().ToList().ForEach(et =>
			{
				if (!(et is IEmbeddedElement))
					throw new ArgumentException("EmbeddedGrouping: One or more elements of embedded group does NOT implement IEmbeddedElement.");
			});
			UpdateNodesBelongingToEmbeddedElements();
		}

		private void UpdateNodesBelongingToEmbeddedElements()
		{
			IEmbeddedDOFInHostTransformationVector transformer;
			if (hasEmbeddedRotations)
				transformer = new Hexa8TranslationAndRotationTransformationVector();
			else
				transformer = new Hexa8LAndNLTranslationTransformationVector();

			foreach (var embeddedElement in embeddedGroup)
			{
				var elType = (IEmbeddedElement)embeddedElement;
				foreach (var node in embeddedElement.Nodes.Skip(8))
				{
					var embeddedNodes = hostGroup
						.Select(e => ((IEmbeddedHostElement)e).BuildHostElementEmbeddedNode(e, node, transformer))
						.Where(e => e != null);
					foreach (var embeddedNode in embeddedNodes)
					{
						if (elType.EmbeddedNodes.Count(x => x.Node == embeddedNode.Node) == 0)
							elType.EmbeddedNodes.Add(embeddedNode);

						// Update embedded node information for elements that are not inside the embedded group but contain an embedded node.
						//foreach (var element in model.ElementsDictionary.Values.Except(embeddedGroup))
						foreach (var subdomain in model.EnumerateSubdomains())
						{
							foreach (var element in model.EnumerateElements(subdomain.ID).Except(embeddedGroup))
							{
								if (element is IEmbeddedElement && element.Nodes.Contains(embeddedNode.Node))
								{
									var currentElementType = (IEmbeddedElement)element;
									if (!currentElementType.EmbeddedNodes.Contains(embeddedNode))
									{
										currentElementType.EmbeddedNodes.Add(embeddedNode);
										element.DofEnumerator = new CohesiveElementEmbedder(element, transformer);
									}
								}
							}
						}
					}
				}

				embeddedElement.DofEnumerator = new CohesiveElementEmbedder(embeddedElement, transformer);
			}
		}
	}
}
