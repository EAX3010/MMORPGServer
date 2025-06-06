using System.Numerics;

namespace MMORPGServer.Game.Entities.Roles
{
    public abstract class MapObject
    {
        public uint Id { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 TargetPosition { get; set; }
        public byte Direction { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsMoving { get; protected set; }

        // Movement properties
        public float MoveSpeed { get; set; } = 2.0f; // Tiles per second
        public float CurrentSpeed { get; protected set; }

        protected MapObject(uint id)
        {
            Id = id;
            Position = Vector2.Zero;
            TargetPosition = Vector2.Zero;
            Direction = 0;
            CurrentSpeed = MoveSpeed;
        }
        public virtual void Update(float deltaTime)
        {
            if (IsMoving)
            {
                UpdateMovement(deltaTime);
            }
        }

        protected virtual void UpdateMovement(float deltaTime)
        {
            var direction = TargetPosition - Position;
            var distance = direction.Length();

            if (distance < 0.1f)
            {
                IsMoving = false;
                return;
            }

            direction = Vector2.Normalize(direction);
            Position += direction * CurrentSpeed * deltaTime;

            // Update direction based on movement
            UpdateDirection(direction);
        }

        protected void UpdateDirection(Vector2 movementDirection)
        {
            // Convert movement direction to isometric direction (0-7)
            float angle = MathF.Atan2(movementDirection.Y, movementDirection.X);
            Direction = (byte)((int)((angle + MathF.PI) * 4 / MathF.PI) % 8);
        }

        public virtual void MoveTo(Vector2 targetPosition)
        {
            TargetPosition = targetPosition;
            IsMoving = true;
        }

        // Convert isometric position to screen coordinates
        public Vector2 ToScreenPosition()
        {
            // Isometric projection formula
            float screenX = (Position.X - Position.Y) * 32; // 32 is half tile width
            float screenY = (Position.X + Position.Y) * 16; // 16 is half tile height
            return new Vector2(screenX, screenY);
        }

        // Convert screen coordinates to isometric position
        public static Vector2 FromScreenPosition(Vector2 screenPos)
        {
            float isoX = (screenPos.X / 32 + screenPos.Y / 16) / 2;
            float isoY = (screenPos.Y / 16 - screenPos.X / 32) / 2;
            return new Vector2(isoX, isoY);
        }
    }
}
