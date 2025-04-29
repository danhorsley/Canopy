// Game1.cs - Updated with more complex L-System rules and both above-ground and below-ground parts
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using CanopyGame.Systems.LSystem;
using CanopyGame.Systems.Rendering;
using CanopyGame.Systems.Input;
using System;
using System.Collections.Generic;


namespace CanopyGame
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
        
        // L-system iteration control
        private int _currentIteration = 3;
        private KeyboardState _previousKeyboardState;
        
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
            
            // Plant rule system including both above-ground and below-ground parts
            var rules = new Dictionary<char, string>
            {
                // Above ground part - stem with branches and leaves
                { 'X', "F+[[X]-X]-F[-FX]+X" },
                { 'F', "FF" },
                
                // Below ground part - root system
                { 'Y', "TR[-TY][+TY]" },
                
                // Leaf pattern
                { 'Z', "L[+L][-L]" }
            };
            
            // Main axiom combines both parts
            string axiom = "S[X]T[Y]";
            
            _lSystem = new LSystem(axiom, rules, _currentIteration);
            
            // Create the interpreter
            _interpreter = new LSystemInterpreter(
                angle: MathHelper.ToRadians(25),
                initialLength: 5f,
                lengthReduction: 0.8f,
                initialWidth: 2.0f
            );
            
            // Create the renderer
            _plantRenderer = new PlantRenderer(GraphicsDevice);
            
            // Create the camera
            _camera = new Camera(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            
            // Set camera starting position to see both above and below ground
            _camera.Zoom = 0.5f;
            
            // Create the input manager
            _inputManager = new InputManager();
            
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Load the font
            _font = Content.Load<SpriteFont>("Arial");
            
            // Generate the plant
            RegeneratePlant();
        }
        
        // Track our plant growth state
        private Dictionary<char, string> _currentRules;
        private string _currentAxiom = "S[X]T[Y]";
        private List<PlantSegment> _currentSegments = new List<PlantSegment>();
        private int _leafGrowthStage = 0;
        private int _rootGrowthStage = 0;
        
        private void RegeneratePlant()
        {
            _currentRules = new Dictionary<char, string>
            {
                { 'X', "F+[[X]-X]-F[-FX]+X" },
                { 'F', "FF" },
                { 'Y', "TR[-TY][+TY]" },
                { 'Z', "L[+L][-L]" }
            };
            
            _lSystem = new LSystem(_currentAxiom, _currentRules, _currentIteration);
            
            string result = _lSystem.Generate();
            _interpreter.Interpret(result);
            _currentSegments = new List<PlantSegment>(_interpreter.Segments);
            _plantRenderer.UpdatePlant(_currentSegments);
            
            // Reset growth stages when regenerating the whole plant
            _leafGrowthStage = 0;
            _rootGrowthStage = 0;
        }
        
        private void GrowLeaves()
        {
            _leafGrowthStage++;
            
            if (_leafGrowthStage >= 5)
            {
                _isGrowingLeaves = false;
                return;
            }
            
            // Add random leaves to branch endpoints
            Random random = new Random();
            List<PlantSegment> newSegments = new List<PlantSegment>();
            
            foreach (var segment in _currentSegments)
            {
                newSegments.Add(segment);
                
                // Only add leaves to branch endpoints with some randomness
                if (segment.Type == PlantPartType.Branch && random.NextDouble() < 0.3)
                {
                    // Create a leaf at this position
                    float leafAngle = (float)(random.NextDouble() * Math.PI * 2);
                    float leafLength = segment.Width * 3;
                    
                    Vector2 leafDir = new Vector2(
                        (float)Math.Cos(leafAngle),
                        (float)Math.Sin(leafAngle)
                    );
                    
                    newSegments.Add(new PlantSegment
                    {
                        Start = segment.End,
                        End = segment.End + (leafDir * leafLength),
                        Width = segment.Width * 1.5f,
                        Type = PlantPartType.Leaf
                    });
                }
            }
            
            _currentSegments = newSegments;
            _plantRenderer.UpdatePlant(_currentSegments);
        }
        
        private void GrowRoots()
        {
            _rootGrowthStage++;
            
            if (_rootGrowthStage >= 5)
            {
                _isGrowingRoots = false;
                return;
            }
            
            // Add random root extensions
            Random random = new Random();
            List<PlantSegment> newSegments = new List<PlantSegment>();
            
            foreach (var segment in _currentSegments)
            {
                newSegments.Add(segment);
                
                // Only extend roots with some randomness
                if (segment.Type == PlantPartType.Root && random.NextDouble() < 0.3)
                {
                    // Create a new root segment
                    float rootAngle = segment.End.X > segment.Start.X ? 
                        (float)(Math.PI * 0.25) : (float)(Math.PI * 0.75);
                    
                    // Add some randomness to the angle
                    rootAngle += (float)(random.NextDouble() * Math.PI * 0.5 - Math.PI * 0.25);
                    
                    float rootLength = segment.Width * 5;
                    
                    Vector2 rootDir = new Vector2(
                        (float)Math.Cos(rootAngle),
                        (float)Math.Sin(rootAngle)
                    );
                    
                    newSegments.Add(new PlantSegment
                    {
                        Start = segment.End,
                        End = segment.End + (rootDir * rootLength),
                        Width = segment.Width * 0.8f,
                        Type = PlantPartType.Root
                    });
                }
            }
            
            _currentSegments = newSegments;
            _plantRenderer.UpdatePlant(_currentSegments);
        }

        // Growth state tracking
        private bool _isGrowingLeaves = false;
        private bool _isGrowingRoots = false;
        private float _growthTimer = 0f;
        private float _growthInterval = 0.5f; // Time between growth steps

        protected override void Update(GameTime gameTime)
        {
            KeyboardState keyboardState = Keyboard.GetState();
            
            _inputManager.Update();
            
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                keyboardState.IsKeyDown(Keys.Escape))
                Exit();
                
            // Camera controls
            if (keyboardState.IsKeyDown(Keys.W))
                _camera.Position += new Vector2(0, -5);
            if (keyboardState.IsKeyDown(Keys.S))
                _camera.Position += new Vector2(0, 5);
            if (keyboardState.IsKeyDown(Keys.A))
                _camera.Position += new Vector2(-5, 0);
            if (keyboardState.IsKeyDown(Keys.D))
                _camera.Position += new Vector2(5, 0);
                
            if (keyboardState.IsKeyDown(Keys.Q))
                _camera.Zoom *= 1.01f;
            if (keyboardState.IsKeyDown(Keys.E))
                _camera.Zoom *= 0.99f;
                
            // Iteration control for testing
            if (keyboardState.IsKeyDown(Keys.Up) && !_previousKeyboardState.IsKeyDown(Keys.Up))
            {
                _currentIteration = MathHelper.Min(_currentIteration + 1, 6);
                RegeneratePlant();
            }
            if (keyboardState.IsKeyDown(Keys.Down) && !_previousKeyboardState.IsKeyDown(Keys.Down))
            {
                _currentIteration = MathHelper.Max(_currentIteration - 1, 1);
                RegeneratePlant();
            }
            
            // Grow leaves with L key
            if (keyboardState.IsKeyDown(Keys.L) && !_previousKeyboardState.IsKeyDown(Keys.L))
            {
                _isGrowingLeaves = true;
                _growthTimer = 0f;
            }
            
            // Grow roots with R key
            if (keyboardState.IsKeyDown(Keys.R) && !_previousKeyboardState.IsKeyDown(Keys.R))
            {
                _isGrowingRoots = true;
                _growthTimer = 0f;
            }
            
            // Handle growth timers
            if (_isGrowingLeaves || _isGrowingRoots)
            {
                _growthTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                
                if (_growthTimer >= _growthInterval)
                {
                    _growthTimer = 0f;
                    
                    if (_isGrowingLeaves)
                    {
                        GrowLeaves();
                    }
                    
                    if (_isGrowingRoots)
                    {
                        GrowRoots();
                    }
                }
            }
            
            _previousKeyboardState = keyboardState;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.SkyBlue);
            
            // Draw a simple ground line
            _spriteBatch.Begin();
            DrawGroundLine();
            
            // Draw growth UI help text
            DrawHelpText();
            _spriteBatch.End();
            
            // Draw the plant
            _plantRenderer.Draw(_camera.GetViewMatrix(), _camera.GetProjectionMatrix());
            
            base.Draw(gameTime);
        }
        
        private void DrawHelpText()
        {
            if (_spriteBatch == null) return;
            
            // Create a simple font if we don't have one
            if (_font == null)
            {
                _font = Content.Load<SpriteFont>("Arial");
            }
            
            string helpText = "Controls: WASD-Move  QE-Zoom  Up/Down-Size  L-Grow Leaves  R-Grow Roots";
            Vector2 textPos = new Vector2(10, 10);
            
            // Draw text with shadow for better visibility
            _spriteBatch.DrawString(_font, helpText, textPos + new Vector2(1, 1), Color.Black);
            _spriteBatch.DrawString(_font, helpText, textPos, Color.White);
            
            // Draw growth status
            string statusText = "";
            if (_isGrowingLeaves) statusText += "Growing leaves... ";
            if (_isGrowingRoots) statusText += "Growing roots... ";
            
            if (!string.IsNullOrEmpty(statusText))
            {
                Vector2 statusPos = new Vector2(10, 40);
                _spriteBatch.DrawString(_font, statusText, statusPos + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_font, statusText, statusPos, Color.LightGreen);
            }
        }
        
        // Font for UI text
        private SpriteFont _font;
        
        private void DrawGroundLine()
        {
            // Draw a simple line to represent the ground - 2/3 above, 1/3 below
            int screenWidth = _graphics.PreferredBackBufferWidth;
            int groundY = (int)(_graphics.PreferredBackBufferHeight * 0.67f);
            
            Texture2D pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.SaddleBrown });
            
            _spriteBatch.Draw(pixel, new Rectangle(0, groundY, screenWidth, 3), Color.SaddleBrown);
        }

        
    }
}