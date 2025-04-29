// Systems/LSystem/LSystem.cs
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Canopy.Systems.LSystem
{
    public class LSystem
    {
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
        }

        public List<Vector2[]> Branches { get; private set; }
        
        private float _angle;
        private float _initialLength;
        private float _lengthReduction;
        private Stack<TurtleState> _stateStack;

        public LSystemInterpreter(float angle, float initialLength, float lengthReduction)
        {
            _angle = angle;
            _initialLength = initialLength;
            _lengthReduction = lengthReduction;
            Branches = new List<Vector2[]>();
            _stateStack = new Stack<TurtleState>();
        }

        public void Interpret(string instructions)
        {
            Branches.Clear();
            
            TurtleState turtle = new TurtleState
            {
                Position = Vector2.Zero,
                Angle = -MathHelper.PiOver2, // Start pointing up
                Length = _initialLength,
                Width = _initialLength * 0.1f
            };

            _stateStack.Clear();

            foreach (char c in instructions)
            {
                switch (c)
                {
                    case 'F': // Draw forward
                        Vector2 oldPos = turtle.Position;
                        turtle.Position += new Vector2(
                            (float)Math.Cos(turtle.Angle) * turtle.Length,
                            (float)Math.Sin(turtle.Angle) * turtle.Length
                        );
                        Branches.Add(new Vector2[] { oldPos, turtle.Position });
                        break;
                    
                    case '+': // Turn right
                        turtle.Angle += _angle;
                        break;
                    
                    case '-': // Turn left
                        turtle.Angle -= _angle;
                        break;
                    
                    case '[': // Save state (start branch)
                        _stateStack.Push(turtle);
                        break;
                    
                    case ']': // Restore state (end branch)
                        turtle = _stateStack.Pop();
                        break;
                        
                    case '>': // Increase length
                        turtle.Length *= 1.1f;
                        break;
                        
                    case '<': // Decrease length
                        turtle.Length *= _lengthReduction;
                        break;
                }
            }
        }
    }
}