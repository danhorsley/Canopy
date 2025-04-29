// Systems/Input/InputManager.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CanopyGame.Systems.Input
{
    public class InputManager
    {
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;
        
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        
        public Vector2 MousePosition => new Vector2(_currentMouseState.X, _currentMouseState.Y);
        
        public void Update()
        {
            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();
            
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
        }
        
        public bool IsKeyPressed(Keys key)
            => _currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
            
        public bool IsMouseButtonPressed(ButtonState buttonState)
            => _currentMouseState.LeftButton == ButtonState.Pressed && 
               _previousMouseState.LeftButton == ButtonState.Released;
    }
}