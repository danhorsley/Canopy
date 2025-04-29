// Systems/LSystem/LSystem.cs - Add plant part type to the system
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CanopyGame.Systems.LSystem
{
    public enum PlantPartType
    {
        Stem,
        Branch,
        Root,
        Leaf
    }

    public struct PlantSegment
    {
        public Vector2 Start;
        public Vector2 End;
        public float Width;
        public PlantPartType Type;
    }

    public class LSystem
    {
        // Existing code...
        public string Axiom { get; private set; }
        public Dictionary<char, string> Rules { get; private set; }
        public int Iterations { get; private set; }

        public LSystem(string axiom, Dictionary<char, string> rules, int iterations)
        {
            Axiom = axiom;
            Rules = rules;
            Iterations = iterations;
        }

        public string Generate()
        {
            string current = Axiom;

            for (int i = 0; i < Iterations; i++)
            {
                string next = "";
                
                foreach (char c in current)
                {
                    if (Rules.ContainsKey(c))
                    {
                        next += Rules[c];
                    }
                    else
                    {
                        next += c;
                    }
                }
                
                current = next;
            }
            
            return current;
        }
    }

    public class LSystemInterpreter
    {
        private struct TurtleState
        {
            public Vector2 Position;
            public float Angle;
            public float Length;
            public float Width;
            public PlantPartType Type;
        }

        public List<PlantSegment> Segments { get; private set; }
        
        private float _angle;
        private float _initialLength;
        private float _lengthReduction;
        private float _initialWidth;
        private Stack<TurtleState> _stateStack;
        private PlantPartType _currentType;

        public LSystemInterpreter(float angle, float initialLength, float lengthReduction, float initialWidth)
        {
            _angle = angle;
            _initialLength = initialLength;
            _lengthReduction = lengthReduction;
            _initialWidth = initialWidth;
            Segments = new List<PlantSegment>();
            _stateStack = new Stack<TurtleState>();
            _currentType = PlantPartType.Stem;
        }

        public void Interpret(string instructions)
        {
            Segments.Clear();
            
            TurtleState turtle = new TurtleState
            {
                Position = Vector2.Zero,
                Angle = -MathHelper.PiOver2, // Start pointing up
                Length = _initialLength,
                Width = _initialWidth,
                Type = PlantPartType.Stem
            };

            _stateStack.Clear();
            _currentType = PlantPartType.Stem;

            foreach (char c in instructions)
            {
                switch (c)
                {
                    case 'F': // Draw forward (stem/branch)
                        Vector2 oldPos = turtle.Position;
                        turtle.Position += new Vector2(
                            (float)Math.Cos(turtle.Angle) * turtle.Length,
                            (float)Math.Sin(turtle.Angle) * turtle.Length
                        );
                        
                        Segments.Add(new PlantSegment 
                        { 
                            Start = oldPos, 
                            End = turtle.Position,
                            Width = turtle.Width,
                            Type = turtle.Type
                        });
                        break;
                    
                    case 'R': // Draw root
                        oldPos = turtle.Position;
                        turtle.Position += new Vector2(
                            (float)Math.Cos(turtle.Angle) * turtle.Length,
                            (float)Math.Sin(turtle.Angle) * turtle.Length
                        );
                        
                        Segments.Add(new PlantSegment 
                        { 
                            Start = oldPos, 
                            End = turtle.Position,
                            Width = turtle.Width,
                            Type = PlantPartType.Root
                        });
                        break;
                    
                    case 'L': // Draw leaf
                        oldPos = turtle.Position;
                        Vector2 leafEnd = oldPos + new Vector2(
                            (float)Math.Cos(turtle.Angle) * turtle.Length * 0.5f,
                            (float)Math.Sin(turtle.Angle) * turtle.Length * 0.5f
                        );
                        
                        Segments.Add(new PlantSegment
                        {
                            Start = oldPos,
                            End = leafEnd,
                            Width = turtle.Width * 2f, // Wider for leaves
                            Type = PlantPartType.Leaf
                        });
                        break;
                    
                    case '+': // Turn right
                        turtle.Angle += _angle;
                        break;
                    
                    case '-': // Turn left
                        turtle.Angle -= _angle;
                        break;
                    
                    case '[': // Save state (start branch)
                        _stateStack.Push(turtle);
                        turtle.Type = PlantPartType.Branch; // New segment is a branch
                        turtle.Width *= 0.8f; // Branches are thinner
                        break;
                    
                    case ']': // Restore state (end branch)
                        turtle = _stateStack.Pop();
                        break;
                        
                    case '>': // Increase length
                        turtle.Length *= 1.1f;
                        break;
                        
                    case '<': // Decrease length
                        turtle.Length *= _lengthReduction;
                        turtle.Width *= _lengthReduction;
                        break;
                        
                    case 'S': // Switch to stem type
                        turtle.Type = PlantPartType.Stem;
                        break;
                        
                    case 'B': // Switch to branch type
                        turtle.Type = PlantPartType.Branch;
                        break;
                        
                    case 'T': // Switch to root type
                        turtle.Type = PlantPartType.Root;
                        break;
                }
            }
        }
    }
}