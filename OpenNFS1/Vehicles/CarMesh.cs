using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenNFS1.Parsers;
using GameEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenNFS1.Vehicles
{
	class CarMesh : Mesh
	{
		Polygon _rightRearWheel, _leftRearWheel, _rightFrontWheel, _leftFrontWheel;
		Polygon _leftBrakeLight, _rightBrakeLight;
		Texture2D _wheelTexture, _brakeOnTexture, _brakeOffTexture;

		static Color BrakeOffColor = new Color(88, 10, 5);
		static Color BrakeOnColor = new Color(158, 110, 6);

		public CarMesh(MeshChunk meshChunk, BitmapChunk bmpChunk, Color brakeColor)
			: base(meshChunk, bmpChunk)
		{
			var wheelPolys = _polys
				.Where(p => p.Label != null && p.TextureName != "rsid")
				.ToList();

			if (wheelPolys.Count >= 4)
			{
				var sortedByZ = wheelPolys.OrderBy(p => p.Vertices.Average(v => v.Z)).ToList();
				var zVals = sortedByZ.Select(p => p.Vertices.Average(v => v.Z)).ToList();

				int splitAfter = 0;
				float maxGap = 0;
				for (int i = 0; i < zVals.Count - 1; i++)
				{
					float gap = zVals[i + 1] - zVals[i];
					if (gap > maxGap) { maxGap = gap; splitAfter = i; }
				}

				var frontGroup = sortedByZ.Take(splitAfter + 1).ToList();
				var rearGroup = sortedByZ.Skip(splitAfter + 1).ToList();

				var frontPair = frontGroup.OrderByDescending(p => Math.Abs(p.Vertices.Average(v => v.X))).Take(2).ToList();
				var rearPair = rearGroup.OrderByDescending(p => Math.Abs(p.Vertices.Average(v => v.X))).Take(2).ToList();

				_leftFrontWheel = frontPair.OrderBy(p => p.Vertices.Average(v => v.X)).First();
				_rightFrontWheel = frontPair.OrderBy(p => p.Vertices.Average(v => v.X)).Last();
				_leftRearWheel = rearPair.OrderBy(p => p.Vertices.Average(v => v.X)).First();
				_rightRearWheel = rearPair.OrderBy(p => p.Vertices.Average(v => v.X)).Last();
			}

			foreach (var poly in _polys)
			{
				if (poly.TextureName == "rsid")
				{
					if (_leftBrakeLight == null) _leftBrakeLight = poly;
					else _rightBrakeLight = poly;
				}
			}
			var tyreEntry = bmpChunk.FindByName("tyr1");
			if (tyreEntry != null)
				_wheelTexture = tyreEntry.Texture;

			var rsidPoly = _polys.FirstOrDefault(a => a.TextureName == "rsid");
			if (rsidPoly != null)
			{
				Color[] pixels = new Color[rsidPoly.Texture.Width * rsidPoly.Texture.Height];
				rsidPoly.Texture.GetData<Color>(pixels);
				for (int i = 0; i < pixels.Length; i++)
					if (pixels[i] == brakeColor) pixels[i] = BrakeOnColor;
				_brakeOnTexture = new Texture2D(Engine.Instance.Device, rsidPoly.Texture.Width, rsidPoly.Texture.Height);
				_brakeOnTexture.SetData<Color>(pixels);
				for (int i = 0; i < pixels.Length; i++)
					if (pixels[i] == BrakeOnColor) pixels[i] = BrakeOffColor;
				_brakeOffTexture = new Texture2D(Engine.Instance.Device, rsidPoly.Texture.Width, rsidPoly.Texture.Height);
				_brakeOffTexture.SetData<Color>(pixels);
			}

			// ── DEBUG: dump all poly info for this mesh ──────────────────────────────
			DumpMeshDebug(meshChunk.Identifier, wheelPolys);
		}

		void DumpMeshDebug(string meshId, List<Polygon> wheelPolys)
		{
			if (!GameConfig.DebugLogging) return;
			try
			{
				var sb = new StringBuilder();
				sb.AppendLine("=== CarMesh: " + meshId + " ===");
				sb.AppendLine("Total polys: " + _polys.Count);
				sb.AppendLine();

				sb.AppendLine("--- ALL LABELLED NON-RSID POLYS (wheel candidates) ---");
				foreach (var p in wheelPolys)
				{
					float avgX = p.Vertices.Average(v => v.X);
					float avgY = p.Vertices.Average(v => v.Y);
					float avgZ = p.Vertices.Average(v => v.Z);
					float sizeY = p.Vertices.Max(v => v.Y) - p.Vertices.Min(v => v.Y);
					sb.AppendLine(string.Format(
						"  label={0,-10} tex={1,-8} shape={2} verts={3} centroid=({4:F2},{5:F2},{6:F2}) sizeY={7:F3}",
						p.Label, p.TextureName, p.Shape, p.Vertices.Length, avgX, avgY, avgZ, sizeY));
				}

				sb.AppendLine();
				sb.AppendLine("--- ASSIGNED WHEEL POLYS ---");
				DumpWheelPoly(sb, "LeftFront", _leftFrontWheel);
				DumpWheelPoly(sb, "RightFront", _rightFrontWheel);
				DumpWheelPoly(sb, "LeftRear", _leftRearWheel);
				DumpWheelPoly(sb, "RightRear", _rightRearWheel);

				sb.AppendLine();
				sb.AppendLine("--- SKIPPED (spurious) LABELLED POLYS ---");
				var assigned = new[] { _leftFrontWheel, _rightFrontWheel, _leftRearWheel, _rightRearWheel };
				foreach (var p in wheelPolys)
				{
					if (!assigned.Contains(p))
					{
						float avgX = p.Vertices.Average(v => v.X);
						float avgZ = p.Vertices.Average(v => v.Z);
						sb.AppendLine(string.Format(
							"  SKIPPED label={0,-10} tex={1,-8} shape={2} centroid=({3:F2},*,{4:F2})",
							p.Label, p.TextureName, p.Shape, avgX, avgZ));
					}
				}

				sb.AppendLine();
				sb.AppendLine("--- ALL POLYS (label|tex|shape|centroid|will_render|texNull) ---");
				var assigned2 = new[] { _leftFrontWheel, _rightFrontWheel, _leftRearWheel, _rightRearWheel };
				foreach (var p in _polys)
				{
					bool isAssignedWheel = assigned2.Contains(p);
					bool skipped = p.Label != null && p.TextureName != "rsid" && !isAssignedWheel;
					float cx = p.Vertices.Average(v => v.X);
					float cy = p.Vertices.Average(v => v.Y);
					float cz = p.Vertices.Average(v => v.Z);
					// Sample centre pixel of texture if available
					string texInfo = "texNULL";
					if (p.Texture != null)
					{
						try
						{
							Color[] tpx = new Color[1];
							p.Texture.GetData(0, new Rectangle(p.Texture.Width / 2, p.Texture.Height / 2, 1, 1), tpx, 0, 1);
							texInfo = p.Texture.Width + "x" + p.Texture.Height
								+ " px=(" + tpx[0].R + "," + tpx[0].G + "," + tpx[0].B + "," + tpx[0].A + ")";
						}
						catch { texInfo = p.Texture.Width + "x" + p.Texture.Height; }
					}
					sb.AppendLine(string.Format(
						"  {0,-12} | tex={1,-8} | {2,-9} | ({3:F2},{4:F2},{5:F2}) | {6,-4} | {7}",
						p.Label == null ? "<null>" : '"' + p.Label + '"',
						p.TextureName ?? "<null>",
						p.Shape,
						cx, cy, cz,
						skipped ? "SKIP" : "DRAW",
						texInfo));
				}

				sb.AppendLine();
				sb.AppendLine("--- WHEEL SIZES ---");
				sb.AppendLine("FrontWheelSize=" + FrontWheelSize);
				sb.AppendLine("RearWheelSize=" + RearWheelSize);
				sb.AppendLine("WheelTexture null=" + (_wheelTexture == null));

				string path = "carmesh_debug_" + meshId + ".txt";
				File.WriteAllText(path, sb.ToString());
			}
			catch { }
		}

		void DumpWheelPoly(StringBuilder sb, string name, Polygon p)
		{
			if (p == null) { sb.AppendLine("  " + name + " = NULL"); return; }
			float avgX = p.Vertices.Average(v => v.X);
			float avgY = p.Vertices.Average(v => v.Y);
			float avgZ = p.Vertices.Average(v => v.Z);
			sb.AppendLine(string.Format(
				"  {0,-12} label={1,-10} tex={2,-8} centroid=({3:F2},{4:F2},{5:F2}) sizeY={6:F3}",
				name, p.Label, p.TextureName,
				avgX, avgY, avgZ,
				p.Vertices.Max(v => v.Y) - p.Vertices.Min(v => v.Y)));
		}

		public Vector3 LeftFrontWheelPos { get { return _leftFrontWheel != null ? GetWheelAxlePoint(_leftFrontWheel) : new Vector3(-1, 0, 2); } }
		public Vector3 RightFrontWheelPos { get { return _rightFrontWheel != null ? GetWheelAxlePoint(_rightFrontWheel) : new Vector3(1, 0, 2); } }
		public Vector3 LeftRearWheelPos { get { return _leftRearWheel != null ? GetWheelAxlePoint(_leftRearWheel) : new Vector3(-1, 0, -2); } }
		public Vector3 RightRearWheelPos { get { return _rightRearWheel != null ? GetWheelAxlePoint(_rightRearWheel) : new Vector3(1, 0, -2); } }

		private Vector3 GetWheelAxlePoint(Polygon wheelPoly)
		{
			if (wheelPoly == null || wheelPoly.Vertices == null || wheelPoly.Vertices.Length == 0)
				return Vector3.Zero;
			float y = (wheelPoly.Vertices.Max(a => a.Y) + wheelPoly.Vertices.Min(a => a.Y)) / 2;
			float z = (wheelPoly.Vertices.Max(a => a.Z) + wheelPoly.Vertices.Min(a => a.Z)) / 2;
			return new Vector3(wheelPoly.Vertices[0].X, y, z);
		}

		public float RearWheelSize
		{
			get
			{
				if (_leftRearWheel == null || _leftRearWheel.Vertices == null || _leftRearWheel.Vertices.Length == 0)
					return 0.5f;
				return _leftRearWheel.Vertices.Max(a => a.Y) - _leftRearWheel.Vertices.Min(a => a.Y);
			}
		}

		public float FrontWheelSize
		{
			get
			{
				if (_leftFrontWheel == null || _leftFrontWheel.Vertices == null || _leftFrontWheel.Vertices.Length == 0)
					return 0.5f;
				return _leftFrontWheel.Vertices.Max(a => a.Y) - _leftFrontWheel.Vertices.Min(a => a.Y);
			}
		}

		// Per-wheel textures — use the actual texture from each wheel polygon (may differ
		// between front/rear or left/right, e.g. Warrior uses tyr4 front and rty4 rear).
		// Fall back to the generic tyr1 _wheelTexture only if the polygon has no texture.
		public Texture2D LeftFrontWheelTexture { get { return (_leftFrontWheel?.Texture ?? _wheelTexture); } }
		public Texture2D RightFrontWheelTexture { get { return (_rightFrontWheel?.Texture ?? _wheelTexture); } }
		public Texture2D LeftRearWheelTexture { get { return (_leftRearWheel?.Texture ?? _wheelTexture); } }
		public Texture2D RightRearWheelTexture { get { return (_rightRearWheel?.Texture ?? _wheelTexture); } }
		public Texture2D WheelTexture { get { return _wheelTexture; } }

		public void Render(Effect effect, bool enableBrakeLights)
		{
			var device = Engine.Instance.Device;
			device.SetVertexBuffer(_vertexBuffer);
			effect.CurrentTechnique.Passes[0].Apply();

			bool IsAssignedWheel(Polygon p) =>
				p == _leftFrontWheel || p == _rightFrontWheel ||
				p == _leftRearWheel || p == _rightRearWheel;

			foreach (Polygon poly in _polys)
			{
				// Skip assigned wheel polys (WheelModel renders those).
				if (IsAssignedWheel(poly)) continue;

				// Skip in-mesh shadow quads — span between inner wheels,
				// render as black rectangles visible from underneath the car.
				if (poly.TextureName == "shad") continue;

				// Skip spurious labelled non-rsid polys (abox, circ, etc.).
				bool isSpurious = (poly.Label != null || poly.TextureName == "abox")
								&& poly.TextureName != "rsid";
				if (isSpurious) continue;

				Texture2D tex;
				if (_brakeOnTexture != null && poly.TextureName == "rsid")
					tex = enableBrakeLights ? _brakeOnTexture : _brakeOffTexture;
				else
					tex = poly.Texture;

				if (tex == null) continue;

				device.Textures[0] = tex;
				device.DrawPrimitives(PrimitiveType.TriangleList, poly.VertexBufferIndex, poly.VertexCount / 3);
			}
		}
	}
}