namespace Area_of_Effect
{
    /// <summary>
    /// Controls which visualization algorithm the VisualizationDispatcherSystem runs.
    /// The integer values are serialized as-is into the JSON payload and must match
    /// the string literals used by the TypeScript frontend exactly.
    /// </summary>
    public enum VisualizationMode
    {
        /// <summary>Zero-overhead baseline — simple radius circle drawn on the overlay buffer.</summary>
        CircleOnly = 0,

        /// <summary>
        /// Renders only the road segments inside the radius, without a background circle.
        /// </summary>
        RoadsOnly = 1,

        /// <summary>
        /// Schedules a heavy <see cref="IsochroneBFSJob"/> that performs a
        /// Breadth-First Search on the Game.Net graph to calculate the true
        /// jagged isochrone boundary polygon.
        /// </summary>
        ExactPolygon = 2,

        /// <summary>
        /// Reads the engine's native cached Game.Net.Node coverage queues for the
        /// selected building's service type and visualises them as a heatmap.
        /// </summary>
        CachedHeatmap = 3,
    }
}
