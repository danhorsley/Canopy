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