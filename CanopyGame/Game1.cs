// Game1.cs - Updated with improved growth algorithms for branches, roots, and leaves
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
                // Modified root rules to grow downward - 180° rotation (TY becomes TR now points down)
                { 'Y', "TR[+TY][-TY]" },
                { 'Z', "L[+L][-L]" }
            };
            
            _lSystem = new LSystem(_currentAxiom, _currentRules, _currentIteration);
            
            string result = _lSystem.Generate();
            _interpreter.Interpret(result);
            _currentSegments = new List<PlantSegment>(_interpreter.Segments);
            
            // Fix initial root orientations - rotate any root segments to point downward
            FixInitialRootOrientation();
            
            _plantRenderer.UpdatePlant(_currentSegments);
            
            // Reset growth stages when regenerating the whole plant
            _leafGrowthStage = 0;
            _rootGrowthStage = 0;
            _branchGrowthStage = 0;
            
            // Clear growth maps when regenerating
            _branchGrowthMap.Clear();
            _leafGrowthMap.Clear();
            _rootGrowthMap.Clear();
        }
        
        private void FixInitialRootOrientation()
        {
            // Find the ground Y position (for reference)
            float groundY = _graphics.PreferredBackBufferHeight * 0.67f;
            
            for (int i = 0; i < _currentSegments.Count; i++)
            {
                PlantSegment segment = _currentSegments[i];
                
                // Only process root segments
                if (segment.Type == PlantPartType.Root)
                {
                    Vector2 direction = segment.End - segment.Start;
                    
                    // Check if root is pointing upward (Y decreasing)
                    if (direction.Y < 0)
                    {
                        // Flip the Y direction to make it point downward
                        Vector2 newEnd = new Vector2(
                            segment.End.X,
                            segment.Start.Y + Math.Abs(direction.Y) // Point downward by same magnitude
                        );
                        
                        // Update the segment
                        _currentSegments[i] = new PlantSegment
                        {
                            Start = segment.Start,
                            End = newEnd,
                            Width = segment.Width,
                            Type = segment.Type
                        };
                    }
                    
                    // Make sure roots start from ground level
                    if (segment.Start.Y < groundY - 5)
                    {
                        // Adjust to start from ground level
                        Vector2 newStart = new Vector2(segment.Start.X, groundY);
                        Vector2 direction2 = segment.End - segment.Start;
                        Vector2 newEnd = newStart + direction2;
                        
                        // Update the segment
                        _currentSegments[i] = new PlantSegment
                        {
                            Start = newStart,
                            End = newEnd,
                            Width = segment.Width,
                            Type = segment.Type
                        };
                    }
                }
            }
        }
        
        // Maps to track where growth has already occurred
        private Dictionary<Vector2, int> _leafGrowthMap = new Dictionary<Vector2, int>();
        private Dictionary<Vector2, int> _branchGrowthMap = new Dictionary<Vector2, int>();
        private Dictionary<Vector2, int> _rootGrowthMap = new Dictionary<Vector2, int>();
        
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

        // Helper to find suitable growth candidates
        private List<PlantSegment> FindSuitableGrowthCandidates(
            PlantPartType type, 
            Dictionary<Vector2, int> growthMap,
            Func<PlantSegment, bool> additionalCriteria = null)
        {
            var candidates = new List<PlantSegment>();
            
            // Diagnostics
            Console.WriteLine($"Finding candidates for {type}, current segments: {_currentSegments.Count}");
            int typeCount = _currentSegments.Count(s => s.Type == type);
            Console.WriteLine($"Segments of type {type}: {typeCount}");
            
            if (typeCount == 0 && type == PlantPartType.Root)
            {
                // Special handling for roots if none exist yet
                Console.WriteLine("No roots found, returning special empty list");
                return candidates; // Return empty to trigger first-time root creation
            }
            
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
                
                // Modified distance check for growth - reduced for branches/leaves to maintain growth
                float minDistance = type == PlantPartType.Root ? 20.0f : 10.0f;
                
                if (isTerminal && !IsTooCloseToExistingGrowth(segment.End, growthMap, minDistance))
                {
                    if (!terminalSegments.ContainsKey(quantizedEnd))
                    {
                        terminalSegments[quantizedEnd] = segment;
                    }
                }
            }
            
            Console.WriteLine($"Found {terminalSegments.Count} terminal segments for {type}");
            
            // Filter by additional criteria if provided
            foreach (var segment in terminalSegments.Values)
            {
                if (additionalCriteria == null || additionalCriteria(segment))
                {
                    candidates.Add(segment);
                }
            }
            
            // For branches and leaves, if we have too few candidates, relax the criteria
            if ((type == PlantPartType.Branch || type == PlantPartType.Leaf) && candidates.Count < 3)
            {
                Console.WriteLine($"Too few candidates for {type}, relaxing criteria");
                
                // Find any suitable segment for growth regardless of terminal status
                foreach (var segment in _currentSegments.Where(s => s.Type == type || 
                                                                (type == PlantPartType.Leaf && s.Type == PlantPartType.Branch) ||
                                                                (type == PlantPartType.Branch && s.Type == PlantPartType.Stem)))
                {
                    // Skip segments that are already candidates
                    if (terminalSegments.ContainsKey(QuantizePosition(segment.End)))
                        continue;
                        
                    // Add as candidate with relaxed growth constraints
                    candidates.Add(segment);
                    
                    // Limit how many we add this way
                    if (candidates.Count >= 5)
                        break;
                }
            }
            
            Console.WriteLine($"Returning {candidates.Count} growth candidates for {type}");
            return candidates;
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
            
            // Simplified initial checks to ensure leaf growth
            bool hasLeafSegments = _currentSegments.Any(s => s.Type == PlantPartType.Leaf);
            bool isFreshStart = _leafGrowthStage == 1;
            
            // First try branches as candidates - natural growth points for leaves
            var leafCandidates = FindSuitableGrowthCandidates(
                PlantPartType.Branch, 
                _leafGrowthMap,
                segment => 
                {
                    // Prioritize higher branches (more sunlight)
                    float normalizedHeight = 1.0f - (segment.End.Y / _graphics.PreferredBackBufferHeight);
                    return random.NextDouble() < (normalizedHeight * 0.8f + 0.2f);
                }
            );
            
            // If no branches or fresh start, add stems as possible leaf growth points
            if (leafCandidates.Count < 2 || isFreshStart)
            {
                var stemCandidates = FindSuitableGrowthCandidates(
                    PlantPartType.Stem,
                    _leafGrowthMap,
                    segment => 
                    {
                        // Only upper stems for leaf growth
                        float normalizedHeight = segment.End.Y / _graphics.PreferredBackBufferHeight;
                        return normalizedHeight < 0.6f; // Stems in upper 60% of screen
                    }
                );
                
                leafCandidates.AddRange(stemCandidates);
            }
            
            // If still no candidates and fresh start, force some leaf growth on any stems/branches
            if (leafCandidates.Count == 0 && isFreshStart)
            {
                foreach (var segment in _currentSegments)
                {
                    if ((segment.Type == PlantPartType.Stem || segment.Type == PlantPartType.Branch) &&
                        segment.End.Y < _graphics.PreferredBackBufferHeight * 0.6f) // Upper 60% of screen
                    {
                        leafCandidates.Add(segment);
                    }
                }
            }
            
            // Choose a subset of candidates based on growth stage and increase count
            int maxLeavesToAdd = Math.Min(leafCandidates.Count, 3 + _leafGrowthStage * 2);
            
            // Sort candidates by height to prioritize sunlight exposure
            leafCandidates.Sort((a, b) => a.End.Y.CompareTo(b.End.Y));
            
            // Diagnostic log for debugging
            Console.WriteLine($"Leaf growth stage: {_leafGrowthStage}, Candidates: {leafCandidates.Count}, Adding: {maxLeavesToAdd}");
            
            for (int i = 0; i < maxLeavesToAdd; i++)
            {
                if (leafCandidates.Count > 0)
                {
                    // Pick candidates prioritizing higher positions
                    int index = (int)(Math.Pow(random.NextDouble(), 2) * leafCandidates.Count);
                    var segment = leafCandidates[index];
                    leafCandidates.RemoveAt(index);
                    
                    // Create a cluster of leaves at this position - more leaves per cluster in later stages
                    int baseLeafCount = 2 + _leafGrowthStage / 2;
                    int leafCount = random.Next(baseLeafCount, baseLeafCount + 2);
                    
                    // Distribute leaves in a more natural pattern around the branch
                    float baseAngle = (float)(random.NextDouble() * Math.PI * 2);
                    float spreadFactor = 0.6f; // Controls how spread out the leaf cluster is
                    
                    for (int j = 0; j < leafCount; j++)
                    {
                        // More natural leaf distribution pattern
                        float angleStep = MathHelper.TwoPi / leafCount;
                        float leafAngle = baseAngle + (j * angleStep) + 
                                       (float)(random.NextDouble() - 0.5) * angleStep * spreadFactor;
                        
                        // Make leaves bigger in later stages
                        float leafLength = segment.Width * (2.0f + (float)(_leafGrowthStage * 0.5f) + 
                                                          (float)random.NextDouble() * 2.0f);
                        
                        Vector2 leafDir = new Vector2(
                            (float)Math.Cos(leafAngle),
                            (float)Math.Sin(leafAngle)
                        );
                        
                        // Stronger upward bias for leaves
                        leafDir.Y -= 0.3f;
                        leafDir.Normalize();
                        
                        Vector2 leafEnd = segment.End + (leafDir * leafLength);
                        
                        // Make leaves thicker and more visible
                        newSegments.Add(new PlantSegment
                        {
                            Start = segment.End,
                            End = leafEnd,
                            Width = segment.Width * (1.0f + 0.1f * _leafGrowthStage), // Thicker in later stages
                            Type = PlantPartType.Leaf
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
            
            // Diagnostic logging
            Console.WriteLine($"Root growth stage: {_rootGrowthStage}");
            
            if (_rootGrowthStage >= 5)
            {
                _isGrowingRoots = false;
                return;
            }
            
            Random random = new Random();
            List<PlantSegment> newSegments = new List<PlantSegment>(_currentSegments);
            
            // Get ground Y position
            float groundY = _graphics.PreferredBackBufferHeight * 0.67f;
            
            // Create initial roots if first stage and none exist
            if (_rootGrowthStage == 1)
            {
                bool hasRoots = _currentSegments.Any(s => s.Type == PlantPartType.Root);
                if (!hasRoots)
                {
                    Console.WriteLine("No roots found - creating initial roots");
                    
                    // Find all stems that are near the ground
                    var stemCandidates = _currentSegments
                        .Where(s => s.Type == PlantPartType.Stem && 
                               Math.Abs(s.End.Y - groundY) < 20)
                        .ToList();
                    
                    if (stemCandidates.Count > 0)
                    {
                        // Pick 1-3 stems to start roots from
                        int rootStarts = Math.Min(3, stemCandidates.Count);
                        Console.WriteLine($"Found {stemCandidates.Count} stem candidates, creating {rootStarts} root starts");
                        
                        for (int i = 0; i < rootStarts; i++)
                        {
                            int index = random.Next(stemCandidates.Count);
                            var segment = stemCandidates[index];
                            stemCandidates.RemoveAt(index);
                            
                            // Create 2-3 initial roots from this stem
                            int rootCount = 2 + random.Next(2);
                            
                            for (int j = 0; j < rootCount; j++)
                            {
                                // Downward growth with spread - between 45° and 135° (π/4 and 3π/4)
                                float rootAngle = MathHelper.PiOver2 + // Straight down
                                               (float)(random.NextDouble() - 0.5) * MathHelper.PiOver2; // ±45° spread
                                
                                float rootLength = segment.Width * (4.0f + random.Next(3));
                                
                                Vector2 rootStart = new Vector2(segment.End.X, groundY);
                                
                                Vector2 rootDir = new Vector2(
                                    (float)Math.Cos(rootAngle),
                                    (float)Math.Sin(rootAngle)
                                );
                                
                                Vector2 rootEnd = rootStart + (rootDir * rootLength);
                                
                                newSegments.Add(new PlantSegment
                                {
                                    Start = rootStart,
                                    End = rootEnd,
                                    Width = segment.Width * 0.8f,
                                    Type = PlantPartType.Root
                                });
                                
                                // Track where we added roots
                                TrackGrowthPosition(rootStart, _rootGrowthMap);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No stem candidates found near ground");
                        
                        // If no stems near ground, create roots from center of ground line
                        Vector2 rootStart = new Vector2(_graphics.PreferredBackBufferWidth / 2, groundY);
                        
                        // Create 3 initial roots from center of ground
                        for (int j = 0; j < 3; j++)
                        {
                            // Downward growth with spread
                            float rootAngle = MathHelper.PiOver2 + 
                                           (float)(random.NextDouble() - 0.5) * MathHelper.PiOver2;
                            
                            float rootLength = 10.0f + random.Next(10);
                            
                            Vector2 rootDir = new Vector2(
                                (float)Math.Cos(rootAngle),
                                (float)Math.Sin(rootAngle)
                            );
                            
                            Vector2 rootEnd = rootStart + (rootDir * rootLength);
                            
                            newSegments.Add(new PlantSegment
                            {
                                Start = rootStart,
                                End = rootEnd,
                                Width = 2.0f,
                                Type = PlantPartType.Root
                            });
                            
                            // Track where we added roots
                            TrackGrowthPosition(rootStart, _rootGrowthMap);
                        }
                    }
                }
            }
            
            // Find suitable root endpoints for further growth
            var rootCandidates = FindSuitableGrowthCandidates(
                PlantPartType.Root, 
                _rootGrowthMap,
                segment => 
                {
                    // Accept any root endpoint for growth
                    return true;
                }
            );
            
            Console.WriteLine($"Found {rootCandidates.Count} root candidates for growth");
            
            // Choose a subset of candidates
            int maxRootsToAdd = Math.Min(rootCandidates.Count, 2 + _rootGrowthStage);
            
            for (int i = 0; i < maxRootsToAdd; i++)
            {
                if (rootCandidates.Count > 0)
                {
                    int index = random.Next(rootCandidates.Count);
                    var segment = rootCandidates[index];
                    rootCandidates.RemoveAt(index);
                    
                    // Create 1-3 new root segments
                    int rootCount = 1 + random.Next(2);
                    
                    for (int j = 0; j < rootCount; j++)
                    {
                        // Ensure downward growth with slight horizontal spread
                        float rootAngle = MathHelper.PiOver2 + 
                                       (float)(random.NextDouble() - 0.5) * MathHelper.PiOver2;
                        
                        float rootLength = segment.Width * (3.0f + random.Next(3));
                        
                        Vector2 rootDir = new Vector2(
                            (float)Math.Cos(rootAngle),
                            (float)Math.Sin(rootAngle)
                        );
                        
                        Vector2 rootEnd = segment.End + (rootDir * rootLength);
                        
                        newSegments.Add(new PlantSegment
                        {
                            Start = segment.End,
                            End = rootEnd,
                            Width = segment.Width * 0.8f,
                            Type = PlantPartType.Root
                        });
                        
                        // Track where we added roots
                        TrackGrowthPosition(segment.End, _rootGrowthMap);
                    }
                }
            }
            
            _currentSegments = newSegments;
            _plantRenderer.UpdatePlant(_currentSegments);
        }
        
        private void GrowBranches()
        {
            _branchGrowthStage++;
            
            if (_branchGrowthStage >= 5)
            {
                _isGrowingBranches = false;
                return;
            }
            
            Random random = new Random();
            List<PlantSegment> newSegments = new List<PlantSegment>(_currentSegments);
            
            Console.WriteLine($"Branch growth stage: {_branchGrowthStage}");
            
            // Get the latest generation branches - prioritize newer growth points
            var recentBranches = new List<PlantSegment>();
            var olderBranches = new List<PlantSegment>();
            
            // Separate branches into recent and older groups based on position in list
            // (Assuming segments added later in the list are newer)
            for (int i = _currentSegments.Count - 1; i >= 0; i--)
            {
                var segment = _currentSegments[i];
                if (segment.Type == PlantPartType.Branch || segment.Type == PlantPartType.Stem)
                {
                    if (recentBranches.Count < 10) // Limit to 10 most recent
                    {
                        // Check if this is a terminal segment
                        bool isTerminal = true;
                        Vector2 quantizedEnd = QuantizePosition(segment.End);
                        
                        foreach (var otherSegment in _currentSegments)
                        {
                            if (Vector2.Distance(quantizedEnd, QuantizePosition(otherSegment.Start)) < 0.1f)
                            {
                                isTerminal = false;
                                break;
                            }
                        }
                        
                        if (isTerminal)
                        {
                            recentBranches.Add(segment);
                        }
                    }
                    else
                    {
                        olderBranches.Add(segment);
                    }
                }
            }
            
            Console.WriteLine($"Found {recentBranches.Count} recent branches, {olderBranches.Count} older branches");
            
            // Prioritize branching from the newest growth points
            var branchCandidates = new List<PlantSegment>();
            
            // Add recent terminal branches with high priority
            foreach (var segment in recentBranches)
            {
                if (!IsTooCloseToExistingGrowth(segment.End, _branchGrowthMap, 15.0f))
                {
                    branchCandidates.Add(segment);
                }
            }
            
            // If we need more candidates, add some older branches/stems
            if (branchCandidates.Count < 3 && olderBranches.Count > 0)
            {
                // Sort older branches by height - prefer branching higher up
                olderBranches.Sort((a, b) => a.End.Y.CompareTo(b.End.Y));
                
                // Add a subset of older branches, focusing on higher ones
                int maxOlder = Math.Min(5, olderBranches.Count);
                for (int i = 0; i < maxOlder; i++)
                {
                    if (!IsTooCloseToExistingGrowth(olderBranches[i].End, _branchGrowthMap, 10.0f))
                    {
                        branchCandidates.Add(olderBranches[i]);
                    }
                }
            }
            
            // Fallback - if still no candidates, find any valid branching points
            if (branchCandidates.Count == 0)
            {
                // Find suitable stem/branch endpoints for new branching
                branchCandidates = FindSuitableGrowthCandidates(
                    PlantPartType.Stem, 
                    _branchGrowthMap,
                    segment => 
                    {
                        // Prioritize mid-height stems for branching with higher probability
                        float normalizedHeight = segment.End.Y / _graphics.PreferredBackBufferHeight;
                        float heightFactor = 1.0f - 2.0f * Math.Abs(normalizedHeight - 0.5f);
                        return random.NextDouble() < Math.Max(0.4f, heightFactor);
                    }
                );
                
                // Also consider branches as potential candidates
                var additionalCandidates = FindSuitableGrowthCandidates(
                    PlantPartType.Branch, 
                    _branchGrowthMap,
                    segment => 
                    {
                        // Increased probability of sub-branching for visibility
                        return random.NextDouble() < 0.5f;
                    }
                );
                
                branchCandidates.AddRange(additionalCandidates);
            }
            
            // Choose a subset of candidates with increased count
            int baseBranchCount = 2 + _branchGrowthStage;
            int maxBranchesToAdd = Math.Min(branchCandidates.Count, baseBranchCount);
            
            Console.WriteLine($"Selected {maxBranchesToAdd} branch candidates from a pool of {branchCandidates.Count}");
            
            for (int i = 0; i < maxBranchesToAdd; i++)
            {
                if (branchCandidates.Count > 0)
                {
                    int index = random.Next(branchCandidates.Count);
                    var segment = branchCandidates[index];
                    branchCandidates.RemoveAt(index);
                    
                    // Determine the primary direction of the segment
                    Vector2 segmentDir = Vector2.Normalize(segment.End - segment.Start);
                    
                    // Calculate the natural continuation direction - slightly upward from current
                    float baseAngle = (float)Math.Atan2(segmentDir.Y, segmentDir.X);
                    
                    // Adjust base angle to have an upward bias
                    if (baseAngle > 0 && baseAngle < MathHelper.Pi)
                    {
                        // If pointing downward, adjust more toward horizontal
                        baseAngle -= MathHelper.ToRadians(20);
                    }
                    else if (baseAngle < 0 && baseAngle > -MathHelper.PiOver2)
                    {
                        // If pointing upward-left, adjust more upward
                        baseAngle -= MathHelper.ToRadians(10);
                    }
                    else if (baseAngle > -MathHelper.Pi && baseAngle < -MathHelper.PiOver2)
                    {
                        // If pointing upward-right, adjust more upward
                        baseAngle += MathHelper.ToRadians(10);
                    }
                    
                    // Create 1-2 branches with natural angles
                    int branchCount = 1 + (int)(random.NextDouble() * 2);
                    
                    for (int j = 0; j < branchCount; j++)
                    {
                        float branchAngle;
                        
                        if (j == 0)
                        {
                            // Main continuation branch - small deviation from current direction
                            branchAngle = baseAngle + MathHelper.ToRadians(-10 + random.Next(20));
                        }
                        else
                        {
                            // Side branch - larger deviation (30-60 degrees)
                            bool goLeft = random.Next(2) == 0;
                            float deviation = MathHelper.ToRadians(30 + random.Next(30)) * (goLeft ? -1 : 1);
                            branchAngle = baseAngle + deviation;
                        }
                        
                        // Ensure the angle is reasonable - avoid downward growth for branches
                        if (branchAngle > 0 && branchAngle < MathHelper.Pi)
                        {
                            branchAngle = Math.Max(-MathHelper.PiOver4, Math.Min(branchAngle, MathHelper.PiOver4));
                        }
                        
                        // Make branches longer for visibility
                        float branchLength = segment.Width * (4.0f + (float)random.NextDouble() * 3.0f);
                        
                        // Slightly reduce length for side branches
                        if (j > 0) branchLength *= 0.8f;
                        
                        Vector2 branchDir = new Vector2(
                            (float)Math.Cos(branchAngle),
                            (float)Math.Sin(branchAngle)
                        );
                        
                        // Ensure upward bias
                        branchDir.Y = Math.Min(branchDir.Y, 0.1f); // Strong limit on downward growth
                        branchDir.Normalize();
                        
                        Vector2 branchEnd = segment.End + (branchDir * branchLength);
                        
                        // Less strict overlap checking
                        bool overlaps = false;
                        foreach (var existingSegment in _currentSegments)
                        {
                            // Skip overlap check with segments that are very close (likely parent segments)
                            if (Vector2.Distance(segment.End, existingSegment.End) < 5.0f ||
                                Vector2.Distance(segment.End, existingSegment.Start) < 5.0f)
                            {
                                continue;
                            }
                            
                            // Only check for intersections with segments that aren't trivially far away
                            if (Vector2.Distance(segment.End, existingSegment.End) < branchLength * 1.5f &&
                                DoLinesIntersect(segment.End, branchEnd, existingSegment.Start, existingSegment.End))
                            {
                                overlaps = true;
                                break;
                            }
                        }
                        
                        // Relaxed overlap checking for first growth stage
                        if (!overlaps || _branchGrowthStage <= 2)
                        {
                            // Make branches thicker for visibility
                            newSegments.Add(new PlantSegment
                            {
                                Start = segment.End,
                                End = branchEnd,
                                Width = segment.Width * (j == 0 ? 0.9f : 0.7f), // Main continuation thicker
                                Type = PlantPartType.Branch
                            });
                            
                            // Track where we added branches
                            TrackGrowthPosition(segment.End, _branchGrowthMap);
                        }
                    }
                }
            }
            
            _currentSegments = newSegments;
            _plantRenderer.UpdatePlant(_currentSegments);
        }
        
        // Helper to check if lines intersect
        private bool DoLinesIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            // Simple bounding box check first for performance
            float a_min_x = Math.Min(a1.X, a2.X);
            float a_max_x = Math.Max(a1.X, a2.X);
            float a_min_y = Math.Min(a1.Y, a2.Y);
            float a_max_y = Math.Max(a1.Y, a2.Y);
            
            float b_min_x = Math.Min(b1.X, b2.X);
            float b_max_x = Math.Max(b1.X, b2.X);
            float b_min_y = Math.Min(b1.Y, b2.Y);
            float b_max_y = Math.Max(b1.Y, b2.Y);
            
            // If bounding boxes don't overlap, lines can't intersect
            if (a_max_x < b_min_x || b_max_x < a_min_x || 
                a_max_y < b_min_y || b_max_y < a_min_y)
            {
                return false;
            }
            
            // Vectors for line segments
            Vector2 a = a2 - a1;
            Vector2 b = b2 - b1;
            Vector2 c = b1 - a1;
            
            // Line intersection formula
            float crossAB = a.X * b.Y - a.Y * b.X;
            
            // If lines are parallel, they don't intersect
            if (Math.Abs(crossAB) < 0.0001f)
            {
                return false;
            }
            
            float t = (c.X * b.Y - c.Y * b.X) / crossAB;
            float u = (c.X * a.Y - c.Y * a.X) / crossAB;
            
            // Check if intersection point is within both line segments
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        // Growth state tracking
        private bool _isGrowingLeaves = false;
        private bool _isGrowingRoots = false;
        private float _growthTimer = 0f;
        private float _growthInterval = 0.5f; // Time between growth steps
        private bool _isGrowingBranches = false;
        private int _branchGrowthStage = 0;

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
            
            // Process the R key for growing roots - separate from other keys to debug
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