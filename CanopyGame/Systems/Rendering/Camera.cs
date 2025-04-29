// Systems/Rendering/Camera.cs
using Microsoft.Xna.Framework;

namespace Canopy.Systems.Rendering
{
    public class Camera
    {
        public Vector2 Position { get; set; }
        public float Zoom { get; set; }
        public float Rotation { get; set; }
        
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        
        public Camera(int screenWidth, int screenHeight)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            Position = Vector2.Zero;
            Zoom = 1.0f;
            Rotation = 0.0f;
        }
        
        public Matrix GetViewMatrix()
        {
            return
                Matrix.CreateTranslation(new Vector3(-Position, 0.0f)) *
                Matrix.CreateRotationZ(Rotation) *
                Matrix.CreateScale(new Vector3(Zoom, Zoom, 1.0f)) *
                Matrix.CreateTranslation(new Vector3(_screenWidth * 0.5f, _screenHeight * 0.5f, 0.0f));
        }
        
        public Matrix GetProjectionMatrix()
        {
            return Matrix.CreateOrthographicOffCenter(
                0, _screenWidth, _screenHeight, 0, 0, 1);
        }
    }
}