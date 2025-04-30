// Game1.cs - Completely revised with simplified L-System and fully procedural growth
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using CanopyGame.Systems.LSystem;
using CanopyGame.Systems.Rendering;
using CanopyGame.Systems.Input;
using System;
using System.Collections.Generic;
using System.Linq;

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
        
        // Maps to track where growth has already occurred
        private Dictionary<Vector2, int> _leafGrowthMap = new Dictionary<Vector2, int>();
        private Dictionary<Vector2, int> _branchGrowthMap = new Dictionary<Vector2, int>();
        private Dictionary<Vector2, int> _rootGrowthMap = new Dictionary<Vector2, int>();
        
        // Growth state tracking
        private bool _isGrowingLeaves = false;
        private bool _isGrowingRoots = false;
        private bool _isGrowingBranches = false;
        private int _leafGrowthStage = 0;
        private int _rootGrowthStage = 0;
        private int _branchGrowthStage = 0;
        private float _growthTimer = 0f;
        private float _growthInterval = 0.5f; // Time between growth steps
        
        // Growth cycle counter to age segments
        private int _globalGrowthCycle = 0;
        
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
            
            // SIMPLIFIED L-SYSTEM: Only generates the trunk skeleton
            // No branches or roots in the L-system - those are fully procedural
            var rules = new Dictionary<char, string>
            {
                // Simplified L-system rule for the main trunk with minimal branching
                { 'X', "F[+X][-X]FX" } // Simpler and shallower branching
            };
            
            // Main axiom is just for the trunk system
            string axiom = "SX";
            
            _lSystem = new LSystem(axiom, rules, _currentIteration);
            
            // Create the interpreter
            _interpreter = new LSystemInterpreter(
                angle: MathHelper.ToRadians(20), // Reduced angle for more vertical growth
                initialLength: 5f,
                lengthReduction: 0.85f, // Less reduction for longer branches
                initialWidth: 2.5f // Slightly thicker trunk
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
        private string _currentAxiom = "SX";
        private List<PlantSegment> _currentSegments = new List<PlantSegment>();
        
        private void RegeneratePlant()
        {
            _currentRules = new Dictionary<char, string>
            {
                // Simplified L-system rule for trunk only
                { 'X', "F[+X][-X]FX" }
            };
            
            _lSystem = new LSystem(_currentAxiom, _currentRules, _currentIteration);
            
            string result = _lSystem.Generate();
            _interpreter.Interpret(result);
            
            // Create a fresh list of segments from the L-system
            _currentSegments = new List<PlantSegment>(_interpreter.Segments);
            
            // Reset all growth counters
            _globalGrowthCycle = 0;
            _leafGrowthStage = 0;
            _rootGrowthStage = 0;
            _branchGrowthStage = 0;
            
            // Clear growth maps
            _branchGrowthMap.Clear();
            _leafGrowthMap.Clear();
            _rootGrowthMap.Clear();
            
            // Now add roots procedurally based on where stems meet the ground
            InitializeRoots();
            
            // Update the renderer
            _plantRenderer.UpdatePlant(_currentSegments);
        }
        
        // Initialize the root system procedurally
        private void InitializeRoots()
        {
            Random random = new Random();
            float groundY = _graphics.PreferredBackBufferHeight * 0.67f;
            
            // Find stem segments that are near ground level
            var groundPoints = new Dictionary<float, Vector2>();
            
            foreach (var segment in _currentSegments)
            {
                // Look for stem segments that cross or are near the ground
                if (segment.Type == PlantPartType.Stem)
                {
                    // If segment crosses the ground
                    if ((segment.Start.Y <= groundY && segment.End.Y >= groundY) ||
                        (segment.Start.Y >= groundY && segment.End.Y <= groundY))
                    {
                        // Find the X position where it crosses
                        float ratio = (groundY - segment.Start.Y) / (segment.End.Y - segment.Start.Y);
                        float xPos = segment.Start.X + ratio * (segment.End.X - segment.Start.X);
                        
                        // Add as a ground contact point
                        if (!groundPoints.ContainsKey(xPos))
                        {
                            groundPoints[xPos] = new Vector2(xPos, groundY);
                        }
                    }
                    // Or if segment endpoint is close to ground
                    else if (Math.Abs(segment.End.Y - groundY) < 15)
                    {
                        if (!groundPoints.ContainsKey(segment.End.X))
                        {
                            groundPoints[segment.End.X] = new Vector2(segment.End.X, groundY);
                        }
                    }
                }
            }
            
            // If no ground points found, add one at the lowest stem
            if (groundPoints.Count == 0)
            {
                PlantSegment lowestStem = _currentSegments
                    .Where(s => s.Type == PlantPartType.Stem)
                    .OrderByDescending(s => s.End.Y)
                    .FirstOrDefault();
                
                if (lowestStem.End != Vector2.Zero)
                {
                    Vector2 groundPoint = new Vector2(lowestStem.End.X, groundY);
                    groundPoints[groundPoint.X] = groundPoint;
                    
                    // Add a trunk segment connecting to ground if needed
                    if (lowestStem.End.Y < groundY - 5)
                    {
                        _currentSegments.Add(new PlantSegment
                        {
                            Start = lowestStem.End,
                            End = groundPoint,
                            Width = lowestStem.Width,
                            Type = PlantPartType.Stem,
                            Age = 0,
                            Generation = lowestStem.Generation
                        });
                    }
                }
                else
                {
                    // Fallback if no stems found at all
                    Vector2 groundPoint = new Vector2(_graphics.PreferredBackBufferWidth / 2, groundY);
                    groundPoints[groundPoint.X] = groundPoint;
                }
            }
            
            // Create initial root system from ground points
            foreach (var point in groundPoints.Values)
            {
                int rootCount = 2 + random.Next(3); // 2-4 roots per ground point
                
                for (int i = 0; i < rootCount; i++)
                {
                    // Fan pattern - angle spread based on position in fan
                    float angleSpread = MathHelper.Pi * 0.7f; // 126° spread
                    float baseAngle = MathHelper.PiOver2 - angleSpread/2; // Left side of down
                    float rootAngle = baseAngle + (angleSpread * i / (rootCount - 1));
                    
                    // Add randomness
                    rootAngle += (float)(random.NextDouble() - 0.5) * MathHelper.Pi / 12; // ±15° randomness
                    
                    // Taproot (center) is longer
                    float lengthFactor = 1.0f - 0.6f * Math.Abs((i - (rootCount - 1) / 2.0f) / ((rootCount - 1) / 2.0f));
                    float rootLength = 12.0f + random.Next(8) * lengthFactor;
                    
                    Vector2 rootDir = new Vector2(
                        (float)Math.Cos(rootAngle),
                        (float)Math.Sin(rootAngle)
                    );
                    
                    Vector2 rootEnd = point + (rootDir * rootLength);
                    
                    _currentSegments.Add(new PlantSegment
                    {
                        Start = point,
                        End = rootEnd,
                        Width = 1.5f * (0.5f + 0.5f * lengthFactor), // Thicker center roots
                        Type = PlantPartType.Root,
                        Age = 0,
                        Generation = 0
                    });
                    
                    // Track where we added roots
                    TrackGrowthPosition(point, _rootGrowthMap);
                }
            }
        }
        
        // Helper to quantize positions to avoid floating point comparison issues
        private Vector2 QuantizePosition(Vector2 position, float quantizationStep = 5.0f)
        {
            return new Vector2(
                (float)Math.Round(position.X / quantizationStep) * quantizationStep,
                (float)Math.Round(position.Y / quantizationStep) * quantizationStep
            );
        }
        
        // Helper to check if growth is too close to existing growth
        private bool IsTooCloseToExistingGrowth(Vector2 position, Dictionary<Vector2, int> growthMap, float minDistance = 20.0f)
        {
            Vector2 quantizedPos = QuantizePosition(position);
            
            // For roots, use a special check that only prevents overlapping at the exact same position
            if (growthMap == _rootGrowthMap)
            {
                foreach (var existingPos in growthMap.Keys)
                {
                    // For roots, we only care about very close positions (allow more dense root growth)
                    if (Vector2.Distance(existingPos, quantizedPos) < 5.0f)
                    {
                        return true;
                    }
                }
            }
            else
            {
                foreach (var existingPos in growthMap.Keys)
                {
                    if (Vector2.Distance(existingPos, quantizedPos) < minDistance)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        // Helper to track a new growth position
        private void TrackGrowthPosition(Vector2 position, Dictionary<Vector2, int> growthMap)
        {
            Vector2 quantizedPos = QuantizePosition(position);
            
            if (growthMap.ContainsKey(quantizedPos))
            {
                growthMap[quantizedPos]++;
            }
            else
            {
                growthMap[quantizedPos] = 1;
            }
        }
        
        // Helper to find suitable growth candidates with age prioritization
        private List<PlantSegment> FindGrowthCandidates(
            PlantPartType type, 
            Dictionary<Vector2, int> growthMap,
            Func<PlantSegment, float> scoringFunction = null,
            float minDistance = 20.0f,
            int? maxAge = null)
        {
            var candidates = new List<PlantSegment>();
            
            // Find all terminal segments of the given type
            var terminalSegments = new Dictionary<Vector2, PlantSegment>();
            
            // First pass: collect all endpoints
            var allEndpoints = new HashSet<Vector2>(
                _currentSegments
                .Where(s => s.Type == type)
                .Select(s => QuantizePosition(s.End))
            );
            
            // Second pass: find terminals (endpoints that aren't also start points)
            foreach (var segment in _currentSegments.Where(s => s.Type == type))
            {
                // Skip segments that are too old if maxAge is specified
                if (maxAge.HasValue && segment.Age > maxAge.Value)
                {
                    continue;
                }
                
                Vector2 quantizedEnd = QuantizePosition(segment.End);
                
                // Check if this is a terminal segment (endpoint not used as a start point)
                bool isTerminal = true;
                foreach (var otherSegment in _currentSegments)
                {
                    if (Vector2.Distance(quantizedEnd, QuantizePosition(otherSegment.Start)) < 0.1f)
                    {
                        isTerminal = false;
                        break;
                    }
                }
                
                if (isTerminal && !IsTooCloseToExistingGrowth(segment.End, growthMap, minDistance))
                {
                    if (!terminalSegments.ContainsKey(quantizedEnd))
                    {
                        terminalSegments[quantizedEnd] = segment;
                    }
                }
            }
            
            candidates.AddRange(terminalSegments.Values);
            
            // Sort by scoring function if provided
            if (scoringFunction != null && candidates.Count > 0)
            {
                candidates.Sort((a, b) => scoringFunction(b).CompareTo(scoringFunction(a)));
            }
            
            return candidates;
        }

        private void GrowBranches()
        {
            _branchGrowthStage++;
            _globalGrowthCycle++;
            
            if (_branchGrowthStage >= 5)
            {
                _isGrowingBranches = false;
                return;
            }
            
            Random random = new Random();
            List<PlantSegment> newSegments = new List<PlantSegment>(_currentSegments);
            
            Console.WriteLine($"Branch growth stage: {_branchGrowthStage}");
            
            // Age all existing segments
            for (int i = 0; i < _currentSegments.Count; i++)
            {
                var segment = _currentSegments[i];
                segment.Age++; // Increment age
                _currentSegments[i] = segment;
            }
            
            // Branch growth scoring function - prioritizes:
            // 1. Young segments (newer growth points)
            // 2. Higher positions (upper canopy)
            // 3. Higher generations (out at the tips)
            Func<PlantSegment, float> branchScoreFunc = (segment) => {
                float heightFactor = 1.0f - (segment.End.Y / _graphics.PreferredBackBufferHeight);
                float ageFactor = 1.0f / (segment.Age + 1);
                float genFactor = segment.Generation / 10.0f;
                
                return heightFactor * 0.5f + ageFactor * 0.3f + genFactor * 0.2f;
            };
            
            // Find growth candidates, prioritizing young and high segments
            var stemCandidates = FindGrowthCandidates(
                PlantPartType.Stem,
                _branchGrowthMap,
                branchScoreFunc,
                15.0f,
                maxAge: 3 // Only use relatively new stems
            );
            
            var branchCandidates = FindGrowthCandidates(
                PlantPartType.Branch,
                _branchGrowthMap,
                branchScoreFunc,
                15.0f,
                maxAge: 2 // Prefer even newer branches
            );
            
            // Combine candidates, with preference for branches
            var allCandidates = new List<PlantSegment>();
            allCandidates.AddRange(branchCandidates);
            allCandidates.AddRange(stemCandidates);
            
            // Sort all candidates by score
            allCandidates.Sort((a, b) => branchScoreFunc(b).CompareTo(branchScoreFunc(a)));
            
            Console.WriteLine($"Found {allCandidates.Count} branch candidates (stems: {stemCandidates.Count}, branches: {branchCandidates.Count})");
            
            // Scale branch count based on growth stage
            int maxBranchesToAdd = Math.Min(allCandidates.Count, 2 + _branchGrowthStage);
            
            for (int i = 0; i < maxBranchesToAdd; i++)
            {
                if (allCandidates.Count > 0)
                {
                    // Select from top candidates, with preference for higher scores
                    int index = (int)(Math.Pow(random.NextDouble(), 2) * Math.Min(5, allCandidates.Count));
                    var segment = allCandidates[index];
                    allCandidates.RemoveAt(index);
                    
                    // Calculate the natural growth direction for this segment
                    Vector2 segmentDir = Vector2.Normalize(segment.End - segment.Start);
                    float baseAngle = (float)Math.Atan2(segmentDir.Y, segmentDir.X);
                    
                    // Adjust for natural upward tendency
                    if (baseAngle > 0 && baseAngle < MathHelper.Pi)
                    {
                        baseAngle -= MathHelper.ToRadians(random.Next(10, 30)); // Stronger upward correction
                    }
                    
                    // Create 1-2 branches from this point
                    int branchCount = 1 + (random.NextDouble() < 0.5 ? 1 : 0);
                    
                    for (int j = 0; j < branchCount; j++)
                    {
                        float branchAngle;
                        
                        if (j == 0)
                        {
                            // Main continuation branch - small randomized deviation
                            branchAngle = baseAngle + MathHelper.ToRadians((float)(random.NextDouble() - 0.5) * 30);
                        }
                        else
                        {
                            // Side branch - larger deviation to one side
                            float sideDeviation = random.Next(30, 60);
                            branchAngle = baseAngle + MathHelper.ToRadians(random.Next(2) == 0 ? -sideDeviation : sideDeviation);
                        }
                        
                        // Ensure reasonable growth direction
                        if (branchAngle > 0 && branchAngle < MathHelper.Pi)
                        {
                            branchAngle = Math.Max(-MathHelper.PiOver4, Math.Min(branchAngle, MathHelper.PiOver4));
                        }
                        
                        // Branch length
                        float lengthFactor = j == 0 ? 1.0f : 0.7f; // Main branch is longer
                        float branchLength = segment.Width * (3.0f + random.Next(3)) * lengthFactor;
                        
                        Vector2 branchDir = new Vector2(
                            (float)Math.Cos(branchAngle),
                            (float)Math.Sin(branchAngle)
                        );
                        
                        // Ensure upward tendency
                        branchDir.Y = Math.Min(branchDir.Y, 0.1f);
                        branchDir.Normalize();
                        
                        Vector2 branchEnd = segment.End + (branchDir * branchLength);
                        
                        // Add the new branch segment
                        newSegments.Add(new PlantSegment
                        {
                            Start = segment.End,
                            End = branchEnd,
                            Width = segment.Width * (j == 0 ? 0.85f : 0.7f), // Main branch is thicker
                            Type = PlantPartType.Branch,
                            Age = 0, // New segment starts at age 0
                            Generation = segment.Generation + 1
                        });
                        
                        // Track where we added branches
                        TrackGrowthPosition(segment.End, _branchGrowthMap);
                    }
                }
            }
            
            _currentSegments = newSegments;
            _plantRenderer.UpdatePlant(_currentSegments);
        }
        
        private void GrowLeaves()
        {
            _leafGrowthStage++;
            
            if (_leafGrowthStage >= 5)
            {
                _isGrowingLeaves = false;
                return;
            }
            
            Random random = new Random();
            List<PlantSegment> newSegments = new List<PlantSegment>(_currentSegments);
            
            Console.WriteLine($"Leaf growth stage: {_leafGrowthStage}");
            
            // Leaf growth scoring function - prioritizes:
            // 1. Height (more light exposure)
            // 2. Generation (out at the tips)
            // 3. Young segments (newer growth points)
            Func<PlantSegment, float> leafScoreFunc = (segment) => {
                float heightFactor = 1.0f - (segment.End.Y / _graphics.PreferredBackBufferHeight);
                float genFactor = segment.Generation / 10.0f;
                float ageFactor = 1.0f / (segment.Age + 1);
                
                // More weight to height for leaves (light exposure)
                return heightFactor * 0.6f + genFactor * 0.2f + ageFactor * 0.2f;
            };
            
            // Find suitable branch and stem endpoints for leaf growth
            var branchCandidates = FindGrowthCandidates(
                PlantPartType.Branch,
                _leafGrowthMap,
                leafScoreFunc,
                10.0f // Closer spacing for leaves
            );
            
            // Add stems as candidates if needed
            if (branchCandidates.Count < 3)
            {
                var stemCandidates = FindGrowthCandidates(
                    PlantPartType.Stem,
                    _leafGrowthMap,
                    leafScoreFunc,
                    10.0f
                );
                
                branchCandidates.AddRange(stemCandidates);
            }
            
            Console.WriteLine($"Found {branchCandidates.Count} leaf growth candidates");
            
            // Choose candidates for leaf growth, prioritizing higher positions
            int maxLeavesToAdd = Math.Min(branchCandidates.Count, 3 + _leafGrowthStage);
            
            // Scale leaf density with height - more leaves higher up (more sunlight)
            for (int i = 0; i < maxLeavesToAdd; i++)
            {
                if (branchCandidates.Count > 0)
                {
                    // Weighted selection based on score
                    int index = (int)(Math.Pow(random.NextDouble(), 2) * Math.Min(5, branchCandidates.Count));
                    var segment = branchCandidates[index];
                    branchCandidates.RemoveAt(index);
                    
                    // Create a cluster of leaves at this position
                    int baseLeafCount = 1 + _leafGrowthStage / 2;
                    int leafCount = baseLeafCount + random.Next(2);
                    
                    // Scale leaf density with height
                    float heightRatio = 1.0f - (segment.End.Y / _graphics.PreferredBackBufferHeight);
                    leafCount = (int)(leafCount * (0.5f + heightRatio * 0.5f)) + 1;
                    
                    // Create a leaf cluster with appropriate spread
                    float baseAngle = (float)(random.NextDouble() * MathHelper.TwoPi);
                    float spreadFactor = 0.7f;
                    
                    for (int j = 0; j < leafCount; j++)
                    {
                        // Distribute leaves around the branch
                        float angleStep = MathHelper.TwoPi / leafCount;
                        float leafAngle = baseAngle + (j * angleStep) + 
                                      (float)(random.NextDouble() - 0.5) * angleStep * spreadFactor;
                        
                        // Adjust angle for natural upward orientation
                        leafAngle -= MathHelper.ToRadians(random.Next(5, 20));
                        
                        // Scale leaf size with height (bigger leaves in more light)
                        float sizeFactor = 0.8f + heightRatio * 0.4f;
                        float leafLength = segment.Width * (1.5f + random.Next(2)) * sizeFactor;
                        
                        Vector2 leafDir = new Vector2(
                            (float)Math.Cos(leafAngle),
                            (float)Math.Sin(leafAngle)
                        );
                        
                        // Upward bias for leaves
                        leafDir.Y -= 0.3f;
                        leafDir.Normalize();
                        
                        Vector2 leafEnd = segment.End + (leafDir * leafLength);
                        
                        newSegments.Add(new PlantSegment
                        {
                            Start = segment.End,
                            End = leafEnd,
                            Width = segment.Width * 0.6f * sizeFactor,
                            Type = PlantPartType.Leaf,
                            Age = 0,
                            Generation = segment.Generation + 1
                        });
                        
                        // Track where we added leaves
                        TrackGrowthPosition(segment.End, _leafGrowthMap);
                    }
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
            
            Random random = new Random();
            List<PlantSegment> newSegments = new List<PlantSegment>(_currentSegments);
            
            Console.WriteLine($"Root growth stage: {_rootGrowthStage}");
            
            // Root growth scoring function - prioritizes:
            // 1. Depth (deeper into soil)
            // 2. Young segments (newer growth points)
            // 3. Spread from center (reaching new soil)
            Func<PlantSegment, float> rootScoreFunc = (segment) => {
                float depthFactor = segment.End.Y / _graphics.PreferredBackBufferHeight;
                float ageFactor = 1.0f / (segment.Age + 1);
                
                // Calculate spread from center of screen
                float centerX = _graphics.PreferredBackBufferWidth / 2;
                float normalizedSpread = Math.Min(1.0f, Math.Abs(segment.End.X - centerX) / (centerX * 0.8f));
                
                return depthFactor * 0.4f + ageFactor * 0.4f + normalizedSpread * 0.2f;
            };
            
            // Find suitable root endpoints for further growth
            var rootCandidates = FindGrowthCandidates(
                PlantPartType.Root,
                _rootGrowthMap,
                rootScoreFunc,
                10.0f // Allow denser root growth
            );
            
            Console.WriteLine($"Found {rootCandidates.Count} root growth candidates");
            
            // Choose candidates for root growth
            int maxRootsToAdd = Math.Min(rootCandidates.Count, 2 + _rootGrowthStage);
            
            for (int i = 0; i < maxRootsToAdd; i++)
            {
                if (rootCandidates.Count > 0)
                {
                    // Weighted selection based on score
                    int index = (int)(Math.Pow(random.NextDouble(), 2) * Math.Min(5, rootCandidates.Count));
                    var segment = rootCandidates[index];
                    rootCandidates.RemoveAt(index);
                    
                    // Create 1-2 new root segments
                    int rootCount = 1 + (random.NextDouble() < 0.6 ? 1 : 0);
                    
                    for (int j = 0; j < rootCount; j++)
                    {
                        float rootAngle;
                        
                        // Determine if this is a main continuation root or a side root
                        if (j == 0)
                        {
                            // Main root - continues downward with small deviation
                            rootAngle = MathHelper.PiOver2 + 
                                      MathHelper.ToRadians((float)(random.NextDouble() - 0.5) * 30);
                        }
                        else
                        {
                            // Side root - larger horizontal deviation
                            float sideDeviation = random.Next(30, 60);
                            rootAngle = MathHelper.PiOver2 + 
                                      MathHelper.ToRadians(random.Next(2) == 0 ? -sideDeviation : sideDeviation);
                        }
                        
                        // Ensure downward trend
                        if (rootAngle < 0 || rootAngle > MathHelper.Pi)
                        {
                            rootAngle = MathHelper.PiOver2;
                        }
                        
                        // Root length - varies by type
                        float lengthFactor = j == 0 ? 1.0f : 0.7f;
                        float rootLength = segment.Width * (3.0f + random.Next(2)) * lengthFactor;
                        
                        Vector2 rootDir = new Vector2(
                            (float)Math.Cos(rootAngle),
                            (float)Math.Sin(rootAngle)
                        );
                        
                        Vector2 rootEnd = segment.End + (rootDir * rootLength);
                        
                        newSegments.Add(new PlantSegment
                        {
                            Start = segment.End,
                            End = rootEnd,
                            Width = segment.Width * (j == 0 ? 0.9f : 0.7f), // Main root is thicker
                            Type = PlantPartType.Root,
                            Age = 0,
                            Generation = segment.Generation + 1
                        });
                        
                        // Track where we added roots
                        TrackGrowthPosition(segment.End, _rootGrowthMap);
                    }
                }
            }
            
            _currentSegments = newSegments;
            _plantRenderer.UpdatePlant(_currentSegments);
        }

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
            
            // Process the R key for growing roots
            if (keyboardState.IsKeyDown(Keys.R) && !_previousKeyboardState.IsKeyDown(Keys.R))
            {
                Console.WriteLine("R key pressed - starting root growth");
                _isGrowingRoots = true;
                _rootGrowthStage = 0;
                _growthTimer = 0f;
            }
            
            // Grow leaves with L key
            if (keyboardState.IsKeyDown(Keys.L) && !_previousKeyboardState.IsKeyDown(Keys.L))
            {
                Console.WriteLine("L key pressed - starting leaf growth");
                _isGrowingLeaves = true;
                _leafGrowthStage = 0;
                _growthTimer = 0f;
            }
            
            // Grow branches with B key
            if (keyboardState.IsKeyDown(Keys.B) && !_previousKeyboardState.IsKeyDown(Keys.B))
            {
                Console.WriteLine("B key pressed - starting branch growth");
                _isGrowingBranches = true;
                _branchGrowthStage = 0;
                _growthTimer = 0f;
            }

            // Update growth timers
            if (_isGrowingLeaves || _isGrowingRoots || _isGrowingBranches)
            {
                _growthTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                
                if (_growthTimer >= _growthInterval)
                {
                    _growthTimer = 0f;
                    
                    if (_isGrowingLeaves) GrowLeaves();
                    if (_isGrowingRoots) GrowRoots();
                    if (_isGrowingBranches) GrowBranches();
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
            
            string helpText = "Controls: WASD-Move  QE-Zoom  Up/Down-Size  L-Grow Leaves  R-Grow Roots  B-Grow Branches";
            Vector2 textPos = new Vector2(10, 10);
            
            // Draw text with shadow for better visibility
            _spriteBatch.DrawString(_font, helpText, textPos + new Vector2(1, 1), Color.Black);
            _spriteBatch.DrawString(_font, helpText, textPos, Color.White);
            
            // Draw growth status
            string statusText = "";
            if (_isGrowingLeaves) statusText += "Growing leaves... ";
            if (_isGrowingRoots) statusText += "Growing roots... ";
            if (_isGrowingBranches) statusText += "Growing branches... ";
            
            if (!string.IsNullOrEmpty(statusText))
            {
                Vector2 statusPos = new Vector2(10, 40);
                _spriteBatch.DrawString(_font, statusText, statusPos + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_font, statusText, statusPos, Color.LightGreen);
            }
            
            // Add cycle counter
            string cycleText = $"Growth Cycle: {_globalGrowthCycle}";
            Vector2 cyclePos = new Vector2(10, 70);
            _spriteBatch.DrawString(_font, cycleText, cyclePos + new Vector2(1, 1), Color.Black);
            _spriteBatch.DrawString(_font, cycleText, cyclePos, Color.White);
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