using Elements.Geometry;
using Elements.GeoJSON;
using Elements.Geometry.Interfaces;
using Elements.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elements.Serialization.JSON;
using Elements.Serialization.IFC;

namespace Elements
{
    /// <summary>
    /// A container for elements, element types, materials, and profiles.
    /// </summary>
    public class Model
    {
        private Dictionary<Guid, Material> _materials = new Dictionary<Guid, Material>();
        private Dictionary<Guid, Element> _elements = new Dictionary<Guid, Element>();

        private Dictionary<Guid, ElementType> _elementTypes = new Dictionary<Guid, ElementType>();

        private Dictionary<Guid, Profile> _profiles = new Dictionary<Guid, Profile>();

        private List<string> _extensions = new List<string>();

        /// <summary>
        /// The version of the assembly.
        /// </summary>
        public string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// The origin of the model.
        /// </summary>
        public Position Origin { get; set; }

        /// <summary>
        /// All elements in the Model.
        /// </summary>
        public Dictionary<Guid, Element> Elements
        {
            get { return this._elements; }
        }

        /// <summary>
        /// All Materials in the Model.
        /// </summary>
        public Dictionary<Guid, Material> Materials
        {
            get { return this._materials; }
        }

        /// <summary>
        /// All element types in the Model.
        /// </summary>
        public Dictionary<Guid, ElementType> ElementTypes
        {
            get { return this._elementTypes; }
        }

        /// <summary>
        /// All profiles in the model.
        /// </summary>
        public Dictionary<Guid, Profile> Profiles
        {
            get { return this._profiles; }
        }

        /// <summary>
        /// A collection of extension identifiers which representing
        /// extensions which must be available at the time of
        /// serialization or deserialization.
        /// </summary>
        public IEnumerable<string> Extensions => _extensions;

        /// <summary>
        /// Construct an empty model.
        /// </summary>
        public Model()
        {
            this.Origin = new Position(0, 0);
            AddMaterial(BuiltInMaterials.Edges);
        }

        /// <summary>
        /// Add an element to the model.
        /// </summary>
        /// <param name="element">The element to add to the model.</param>
        /// <exception cref="System.ArgumentException">Thrown when an element 
        /// with the same Id already exists in the model.</exception>
        public void AddElement(Element element)
        {
            if (element == null)
            {
                return;
            }

            if (!this._elements.ContainsKey(element.Id))
            {
                this._elements.Add(element.Id, element);
                GetRootLevelElementData(element);
            }
            else
            {
                throw new ArgumentException("An Element with the same Id already exists in the Model.");
            }

            if (element is IAggregateElements)
            {
                var agg = (IAggregateElements)element;
                AddElements(agg.Elements);
            }

            AddExtension(element.GetType().Assembly.GetName().Name.ToLower());
        }

        /// <summary>
        /// Update an element existing in the model.
        /// </summary>
        /// <param name="element">The element to update in the model.</param>
        /// <exception cref="System.ArgumentException">Thrown when no element 
        /// with the same Id exists in the model.</exception>
        public void UpdateElement(Element element)
        {
            if (element == null)
            {
                return;
            }

            if (this._elements.ContainsKey(element.Id))
            {
                // remove the previous element
                this._elements.Remove(element.Id);
                // Update the element itselft
                this._elements.Add(element.Id, element);
                // Update the root elements
                GetRootLevelElementData(element);
            }
            else
            {
                throw new ArgumentException("No Element with this Id exists in the Model.");
            }

            if (element is IAggregateElements)
            {
                var agg = (IAggregateElements)element;
                UpdateElements(agg.Elements);
            }

            AddExtension(element.GetType().Assembly.GetName().Name.ToLower());
        }

        /// <summary>
        /// Add a collection of elements to the model.
        /// </summary>
        /// <param name="elements">The elements to add to the model.</param>
        public void AddElements(IEnumerable<Element> elements)
        {
            foreach (var e in elements)
            {
                AddElement(e);
            }
        }

        /// <summary>
        /// Update a collection of elements in the model.
        /// </summary>
        /// <param name="elements">The elements to be updated in the model.</param>
        public void UpdateElements(IEnumerable<Element> elements)
        {
            foreach (var e in elements)
            {
                UpdateElement(e);
            }
        }

        /// <summary>
        /// Get an Element by id from the Model.
        /// </summary>
        /// <param name="id">The identifier of the Element.</param>
        /// <returns>An Element or null if no Element can be found 
        /// with the provided id.</returns>
        public Element GetElementById(Guid id)
        {
            if (this._elements.ContainsKey(id))
            {
                return this._elements[id];
            }
            return null;
        }

        /// <summary>
        /// Get the first Element with the specified name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>An Element or null if no Element can be found 
        /// with the provided name.</returns>
        public Element GetElementByName(string name)
        {
            var found = this.Elements.FirstOrDefault(e => e.Value.Name == name);
            if (found.Equals(new KeyValuePair<long, Element>()))
            {
                return null;
            }
            return found.Value;
        }

        /// <summary>
        /// Get a Material by name.
        /// </summary>
        /// <param name="name">The name of the Material.</param>
        /// <returns>A Material or null if no Material with the 
        /// specified id can be found.</returns>
        public Material GetMaterialByName(string name)
        {
            return this._materials.Values.FirstOrDefault(m => m.Name == name);
        }

        /// <summary>
        /// Get an ElementType by name.
        /// </summary>
        /// <param name="name">The name of the ElementType.</param>
        /// <returns>An ElementType or null if no ElementType with 
        /// the specified name can be found.</returns>
        public ElementType GetElementTypeByName(string name)
        {
            return this._elementTypes.Values.FirstOrDefault(et => et.Name == name);
        }

        /// <summary>
        /// Get a Profile by name.
        /// </summary>
        /// <param name="name">The name of the Profile.</param>
        /// <returns>A Profile or null if no Profile with the 
        /// specified name can be found.</returns>
        public Profile GetProfileByName(string name)
        {
            return this._profiles.Values.FirstOrDefault(p => p.Name != null && p.Name == name);
        }

        /// <summary>
        /// Get all Elements of the specified Type.
        /// </summary>
        /// <typeparam name="T">The Type of element to return.</typeparam>
        /// <returns>A collection of Elements of the specified type.</returns>
        public IEnumerable<T> ElementsOfType<T>()
        {
            return this._elements.Values.OfType<T>();
        }

        /// <summary>
        /// Create a model from JSON.
        /// </summary>
        /// <param name="json">The JSON.</param>
        /// <returns>A model.</returns>
        public static Model FromJson(string json)
        {
            return JsonExtensions.FromJson(json);
        }

        /// <summary>
        /// Create a model from IFC.
        /// </summary>
        /// <param name="path">The path to the IFC STEP file.</param>
        /// <param name="idsToConvert">An optional array of string identifiers 
        /// of IFC entities to convert.</param>
        /// <returns>A model.</returns>
        public static Model FromIFC(string path, string[] idsToConvert = null)
        {
            return IFCExtensions.FromIFC(path, idsToConvert);
        }

        internal Model(Dictionary<Guid, Element> elements, Dictionary<Guid,
            Material> materials, Dictionary<Guid, ElementType> elementTypes,
            Dictionary<Guid, Profile> profiles, List<string> extensions)
        {
            this._elements = elements;
            this._materials = materials;
            this._elementTypes = elementTypes;
            this._profiles = profiles;
            this._extensions = extensions;
            AddMaterial(BuiltInMaterials.Edges);
            AddMaterial(BuiltInMaterials.Void);
        }

        private void AddExtension(string extensionId)
        {
            if (!_extensions.Contains(extensionId))
            {
                _extensions.Add(extensionId);
            }
        }

        private void AddMaterial(Material material)
        {
            if (!this._materials.ContainsKey(material.Id))
            {
                this._materials.Add(material.Id, material);
            }
            else
            {
                this._materials[material.Id] = material;
            }
        }

        private void GetRootLevelElementData(IElement element)
        {
            if (element is IMaterial)
            {
                var mat = (IMaterial)element;
                AddMaterial(mat.Material);
            }

            if (element is IProfile)
            {
                var ipp = (IProfile)element;
                if (ipp.Profile != null)
                {
                    AddProfile((Profile)ipp.Profile);
                }
            }

            if (element is IHasOpenings)
            {
                var ho = (IHasOpenings)element;
                if (ho.Openings != null)
                {
                    foreach (var o in ho.Openings)
                    {
                        AddProfile(o.Profile);
                    }
                }
            }

            if (element is IElementType<WallType>)
            {
                var wtp = (IElementType<WallType>)element;
                if (wtp.ElementType != null)
                {
                    AddElementType(wtp.ElementType);
                    foreach (var layer in wtp.ElementType.MaterialLayers)
                    {
                        AddMaterial(layer.Material);
                    }
                }
            }

            if (element is IElementType<FloorType>)
            {
                var ftp = (IElementType<FloorType>)element;
                if (ftp.ElementType != null)
                {
                    AddElementType(ftp.ElementType);
                    foreach (var layer in ftp.ElementType.MaterialLayers)
                    {
                        AddMaterial(layer.Material);
                    }
                }
            }

            if (element is IElementType<StructuralFramingType>)
            {
                var sft = (IElementType<StructuralFramingType>)element;
                if (sft.ElementType != null)
                {
                    AddElementType(sft.ElementType);
                    AddProfile(sft.ElementType.Profile);
                    AddMaterial(sft.ElementType.Material);
                }
            }

            if (element is IAggregateElements)
            {
                var ae = (IAggregateElements)element;
                if (ae.Elements.Count > 0)
                {
                    foreach (var esub in ae.Elements)
                    {
                        GetRootLevelElementData(esub);
                    }
                }
            }
        }

        private void AddElementType(ElementType elementType)
        {
            if (!this._elementTypes.ContainsKey(elementType.Id))
            {
                this._elementTypes.Add(elementType.Id, elementType);
            }
            else
            {
                this._elementTypes[elementType.Id] = elementType;
            }
        }

        private void AddProfile(Profile profile)
        {
            if (!this._profiles.ContainsKey(profile.Id))
            {
                this._profiles.Add(profile.Id, profile);
            }
            else
            {
                this._profiles[profile.Id] = profile;
            }
        }
    }
}