// Systems/Rendering/PlantRenderer.cs - Updated renderer to use different colors
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using CanopyGame.Systems.LSystem;

namespace CanopyGame.Systems.Rendering
{
    public class PlantRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private List<VertexPositionColor> _vertices;
        private List<short> _indices;
        
        // Colors for different plant parts
        private Color _stemColor = new Color(101, 67, 33);  // Brown
        private Color _branchColor = new Color(140, 98, 57); // Light brown
        private Color _rootColor = new Color(83, 53, 10);   // Dark brown
        private Color _leafColor = new Color(34, 139, 34);  // Green

        public PlantRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice);
            _effect.VertexColorEnabled = true;
            
            _vertices = new List<VertexPositionColor>();
            _indices = new List<short>();
        }

        public void UpdatePlant(List<PlantSegment> segments)
        {
            _vertices.Clear();
            _indices.Clear();
            
            short index = 0;
            
            // For each segment, create a line with appropriate color
            foreach (var segment in segments)
            {
                Color color;
                
                // Choose color based on segment type
                switch (segment.Type)
                {
                    case PlantPartType.Stem:
                        color = _stemColor;
                        break;
                    case PlantPartType.Branch:
                        color = _branchColor;
                        break;
                    case PlantPartType.Root:
                        color = _rootColor;
                        break;
                    case PlantPartType.Leaf:
                        color = _leafColor;
                        break;
                    default:
                        color = Color.White;
                        break;
                }
                
                // Start position
                _vertices.Add(new VertexPositionColor(
                    new Vector3(segment.Start, 0),
                    color));
                
                // End position
                _vertices.Add(new VertexPositionColor(
                    new Vector3(segment.End, 0),
                    color));
                
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