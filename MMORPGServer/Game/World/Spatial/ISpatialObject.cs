namespace MMORPGServer.Game.World.Spatial
{
    public interface ISpatialObject
    {
        uint IndexID { get; }
        uint ObjectId { get; }
        uint MapId { get; }
        uint MapDynamicId { get; }
        bool IsActive { get; }
        MapObjectType ObjectType { get; }
        Vector2 Position { get; }
    }
}