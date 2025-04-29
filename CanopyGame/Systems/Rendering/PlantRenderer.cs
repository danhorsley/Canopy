// Systems/LSystem/LSystem.cs and LSystemInterpreter.cs would contain the code I provided earlier

// Systems/Rendering/PlantRenderer.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Canopy.Systems.Rendering
{
    public class PlantRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private List<VertexPositionColor> _vertices;
        private List<short> _indices;

        public PlantRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice);
            _effect.VertexColorEnabled = true;
            
            _vertices = new List<VertexPositionColor>();
            _indices = new List<short>();
        }

        public void UpdatePlant(List<Vector2[]> branches)
        {
            _vertices.Clear();
            _indices.Clear();
            
            short index = 0;
            
            // For each branch, create a simple line
            foreach (var branch in branches)
            {
                // Start position
                _vertices.Add(new VertexPositionColor(
                    new Vector3(branch[0], 0), // Convert Vector2 to Vector3 with z=0
                    Color.SaddleBrown));
                
                // End position
                _vertices.Add(new VertexPositionColor(
                    new Vector3(branch[1], 0),
                    Color.SaddleBrown));
                
                // Connect with line indices
                _indices.Add(index++);
                _indices.Add(index++);
            }
        }

        public void Draw(Matrix view, Matrix projection)
        {
            if (_vertices.Count == 0)
                return;
                
            _effect.View = view;
            _effect.Projection = projection;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.LineList,
                    _vertices.ToArray(),
                    0,
                    _vertices.Count,
                    _indices.ToArray(),
                    0,
                    _indices.Count / 2);
            }
        }
    }
}

// Systems/Input/InputManager.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Canopy.Systems.Input
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

// Game1.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Canopy.Systems.LSystem;
using Canopy.Systems.Rendering;
using Canopy.Systems.Input;
using System.Collections.Generic;

namespace Canopy
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        private LSystem _lSystem;
        private LSystemInterpreter _interpreter;
        private PlantRenderer _plantRenderer;
        private Camera _camera;
        private InputManager _inputManager;
        
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
            
            // Create the L-system with rules for a basic plant
            var rules = new Dictionary<char, string>
            {
                { 'X', "F+[[X]-X]-F[-FX]+X" },
                { 'F', "FF" }
            };
            _lSystem = new LSystem("X", rules, 4);
            
            // Create the interpreter
            _interpreter = new LSystemInterpreter(
                angle: MathHelper.ToRadians(25),
                initialLength: 5f,
                lengthReduction: 0.8f
            );
            
            // Create the renderer
            _plantRenderer = new PlantRenderer(GraphicsDevice);
            
            // Create the camera
            _camera = new Camera(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            _camera.Position = new Vector2(0, -300); // Center the plant
            
            // Create the input manager
            _inputManager = new InputManager();
            
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Generate the plant
            string result = _lSystem.Generate();
            _interpreter.Interpret(result);
            _plantRenderer.UpdatePlant(_interpreter.Branches);
        }

        protected override void Update(GameTime gameTime)
        {
            _inputManager.Update();
            
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();
                
            // Simple controls for the camera
            if (Keyboard.GetState().IsKeyDown(Keys.W))
                _camera.Position += new Vector2(0, -5);
            if (Keyboard.GetState().IsKeyDown(Keys.S))
                _camera.Position += new Vector2(0, 5);
            if (Keyboard.GetState().IsKeyDown(Keys.A))
                _camera.Position += new Vector2(-5, 0);
            if (Keyboard.GetState().IsKeyDown(Keys.D))
                _camera.Position += new Vector2(5, 0);
                
            if (Keyboard.GetState().IsKeyDown(Keys.Q))
                _camera.Zoom *= 1.01f;
            if (Keyboard.GetState().IsKeyDown(Keys.E))
                _camera.Zoom *= 0.99f;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            
            // Draw the plant
            _plantRenderer.Draw(_camera.GetViewMatrix(), _camera.GetProjectionMatrix());
            
            base.Draw(gameTime);
        }
    }
}