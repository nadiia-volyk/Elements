using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Elements.Geometry;

namespace Elements.Spatial.CellComplex
{
    /// <summary>
    /// A face of a cell. Multiple cells can share the same face.
    /// </summary>
    public class Face : CellChild<Polygon>
    {
        /// <summary>
        /// ID of U direction
        /// </summary>
        public long? UId;

        /// <summary>
        /// ID of V direction
        /// </summary>
        public long? VId;

        /// <summary>
        /// Directed edge IDs
        /// </summary>
        public List<long> HalfEdgeIds;

        /// <summary>
        /// Cells that reference this Face
        /// </summary>
        [JsonIgnore]
        public HashSet<Cell> Cells { get; internal set; } = new HashSet<Cell>();

        /// <summary>
        /// Represents a unique Face within a CellComplex.
        /// Is not intended to be created or modified outside of the CellComplex class code.
        /// </summary>
        /// <param name="cellComplex">CellComplex that this Face belongs to</param>
        /// <param name="id"></param>
        /// <param name="halfEdges">List of the HalfEdges that make up this Face</param>
        /// <param name="u">Optional but highly recommended intended U direction for the Face</param>
        /// <param name="v">Optional but highly recommended intended V direction for the Face</param>
        internal Face(CellComplex cellComplex, long id, List<HalfEdge> halfEdges, UV u = null, UV v = null) : base(id, cellComplex)
        {
            this.HalfEdgeIds = halfEdges.Select(ds => ds.Id).ToList();
            if (u != null)
            {
                this.UId = u.Id;
            }
            if (v != null)
            {
                this.VId = v.Id;
            }
        }

        /// <summary>
        /// Used for deserialization only!
        /// </summary>
        [JsonConstructor]
        internal Face(long id, List<long> halfEdgeIds, long? uId = null, long? vId = null) : base(id, null)
        {
            this.Id = id;
            this.HalfEdgeIds = halfEdgeIds;
            this.UId = uId;
            this.VId = vId;
        }

        /// <summary>
        /// Get the geometry for this Face
        /// </summary>
        /// <returns></returns>
        public override Polygon GetGeometry()
        {
            return new Polygon(this.GetVertices().Select(v => v.Value).ToList());
        }

        /// <summary>
        /// Shortest distance to point
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public override double DistanceTo(Vector3 point)
        {
            return point.DistanceTo(this.GetGeometry());
        }

        /// <summary>
        /// Get associated Cells
        /// </summary>
        /// <returns></returns>
        public List<Cell> GetCells()
        {
            return this.Cells.ToList();
        }

        /// <summary>
        /// Get associated HalfEdges
        /// </summary>
        /// <returns></returns>
        public List<HalfEdge> GetHalfEdges()
        {
            return this.HalfEdgeIds.Select(dsId => CellComplex.GetHalfEdge(dsId)).ToList();
        }

        /// <summary>
        /// Get associated Vertices
        /// </summary>
        /// <returns></returns>
        public List<Vertex> GetVertices()
        {
            return this.GetHalfEdges().Select(ds => this.CellComplex.GetVertex(ds.StartVertexId)).ToList();
        }

        /// <summary>
        /// Get associated Edges
        /// </summary>
        /// <returns></returns>
        public List<Edge> GetEdges()
        {
            return this.GetHalfEdges().Select(ds => ds.GetEdge()).ToList();
        }

        /// <summary>
        /// Face lookup hash is edgeIds in ascending order.
        /// We do not directly use the `halfEdgeIds` because they could wind differently on a shared face.
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <returns></returns>
        public static string GetHash(List<HalfEdge> halfEdges)
        {
            var sortedIds = halfEdges.Select(ds => ds.EdgeId).ToList();
            sortedIds.Sort();
            var hash = String.Join(",", sortedIds);
            return hash;
        }

        /// <summary>
        /// Get the normal vector for this Face
        /// </summary>
        /// <returns></returns>
        public Vector3 GetNormal()
        {
            return this.GetGeometry().Normal();
        }

        /// <summary>
        /// Whether this Face is parallel to another Face
        /// </summary>
        /// <param name="face"></param>
        /// <returns></returns>
        public bool IsParallel(Face face)
        {
            return face.GetNormal().Equals(this.GetNormal());
        }

        /// <summary>
        /// Get a list of all neighbors of this face.
        /// A neighbor is defined as a Face which shares any edge.
        /// </summary>
        /// <param name="parallel">If true, only returns faces that are oriented the same way as this face</param>
        /// <param name="includeSharedVertices">If true, includes faces that share a vertex as well as faces that share a edge</param>
        /// <returns></returns>
        public List<Face> GetNeighbors(bool parallel = false, bool includeSharedVertices = false)
        {
            var groupedFaces = includeSharedVertices ? this.GetVertices().Select(v => v.GetFaces()).ToList() : this.GetEdges().Select(s => s.GetFaces()).ToList();
            var faces = groupedFaces.SelectMany(x => x).Distinct().Where(f => f.Id != this.Id).ToList();
            if (parallel)
            {
                return faces.Where(f => this.IsParallel(f)).ToList();
            }
            else
            {
                return faces;
            }
        }

        /// <summary>
        /// Get a list of neighbor faces that share a specific edge
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="parallel">Whether to only return faces that are parallel to this face.</param>
        /// <returns></returns>
        public List<Face> GetNeighbors(Edge edge, bool parallel = false)
        {
            if (!parallel)
            {
                return edge.GetFaces().Where(face => face.Id != this.Id).ToList();
            }
            else
            {
                return edge.GetFaces().Where(face => face.Id != this.Id && this.IsParallel(face)).ToList();
            }
        }

        public Face GetClosestAssociatedFace(Vector3 point, bool parallel = false, bool includeSharedVertices = false)
        {
            var faces = this.GetNeighbors(parallel, includeSharedVertices).Where(f => f.DistanceTo(point) < this.DistanceTo(point)).OrderBy(f => f.DistanceTo(point)).ToList();
            return faces.Count == 0 ? null : faces.First();
        }
    }
}