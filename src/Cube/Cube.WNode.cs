using System;

namespace Cube
{
	public partial class Cube
	{
		public class WNode
		{
			public XNode[] x_axis;
			public int[]   x_volume;
			public int[]   y_size;
			public int[]   x_floor;

			public WNode(int length)
			{
				x_floor = new int[length];
				x_axis = new XNode[length];
				y_size = new int[length];
				x_volume = new int[length];
			}

			public (int x, int y, int z) FindKeyOffset(int key, int countX)
			{
				int x;
				var mid = x = countX - 1;

				while (mid > 3)
				{
					mid /= 2;

					if (key < x_floor[x - mid]) x -= mid;
				}

				while (key < x_floor[x]) 
					x--;

				var (y, z) = x_axis[x].FindKeyOffset(key, y_size[x]);

				return (x, y, z);
			}

			
			public bool FindIndexForward(int index, int w, int volumeW, int countX, ref int total, out Offset? offset)
			{
				if (total + volumeW > index)
				{
					if (index > total + volumeW / 2)
					{
						total += volumeW;
						{
							offset = backward_x(index, countX, ref total, out var xyz)
								? new Offset(w, xyz.x, xyz.y, xyz.z)
								: (Offset?) null;

							return true;
						}
					}

					if (forward_x(index, countX, ref total, out var xyz1))
					{
						offset = new Offset(w, xyz1.x, xyz1.y, xyz1.z);
						return true;
					}
				}

				offset = default;
				return false;
			}

			public bool FindIndexBackward(int index, int w, int volumeW, int countX, ref int total, out Offset? offset)
			{
				if (total - volumeW <= index)
				{
					if (index < total - volumeW / 2)
					{
						total -= volumeW;
						{
							offset = forward_x(index, countX, ref total, out var xyz1)
								? new Offset(w, xyz1.x, xyz1.y, xyz1.z)
								: (Offset?) null;

							return true;
						}
					}

					if (backward_x(index, countX, ref total, out var xyz))
					{
						offset = new Offset(w, xyz.x, xyz.y, xyz.z);
						return true;
					}
				}

				offset = null;
				return false;
			}

			public bool forward_x(int index, int xCount, ref int total, out (int x, int y, int z) offset)
			{
				for (int x = 0; x < xCount; x++)
				{
					var x_node = x_axis[x];

					if (total + x_volume[x] > index)
					{
						if (index > total + x_volume[x] / 2)
						{
							return BackwardY(index, x_node, x, ref total, out offset);
						}

						if (x_node.FindIndexForward(index, y_size[x], ref total, out var yz))
						{
							offset = (x, yz.y, yz.z);
							return true;
						}
					}

					total += x_volume[x];
				}

				offset = default;
				return false;
			}
		    
			public bool backward_x(int index, int xCount, ref int total, out (int x, int y, int z) offset)
			{
				for (var x = xCount - 1; x >= 0; x--)
				{
					var nodeX = x_axis[x];
					if (total - x_volume[x] <= index)
					{
						if (index < total - x_volume[x] / 2)
						{
							return ForwardY(index, nodeX, x, ref total, out offset);
						}

						if (nodeX.FindIndexBackward(index, y_size[x], ref total, out var yz))
						{
							offset = (x, yz.y, yz.z);
							return true;
						}
					}

					total -= x_volume[x];
				}

				offset = default;
				return false;
			}

			private bool ForwardY(int index, XNode nodeX, int x, ref int total, out (int x, int y, int z) offset)
			{
				total -= x_volume[x];

				if (nodeX.FindIndexForward(index, y_size[x], ref total, out var yz))
				{
					offset = (x, yz.y, yz.z);
					return true;
				}

				offset = default;
				return false;
			}

			private bool BackwardY(int index, XNode nodeX, int x, ref int total, out (int x, int y, int z) offset)
			{
				total += x_volume[x];

				if (nodeX.FindIndexBackward(index, y_size[x], ref total, out var yz))
				{
					offset = (x, yz.y, yz.z);
					return true;
				}

				offset = default;
				return false;
			} 

			public XNode InsertNodeX(int x, int countX, int length)
			{
				if (countX % BSC_M == 0 && countX < length)
				{
					Resize(length);
				}

				if (countX != x + 1)
				{
					Move(x, x + 1, countX - x - 1);
				}

				x_axis[x] = new XNode(length);
				return x_axis[x];
			}

			private void Resize(int length)
			{
				Array.Resize(ref x_floor, length);
				Array.Resize(ref x_axis, length);
				Array.Resize(ref x_volume, length);
				Array.Resize(ref y_size, length);
			}

			private void Move(int sourceIndex, int destinationIndex, int length)
			{
				Array.Copy(x_floor, sourceIndex, x_floor, destinationIndex, length);
				Array.Copy(x_axis, sourceIndex, x_axis, destinationIndex, length);
				Array.Copy(x_volume, sourceIndex, x_volume, destinationIndex, length);
				Array.Copy(y_size, sourceIndex, y_size, destinationIndex, length);
			}

			public void SplitY(int x, int y, int length)
			{
				XNode insert_y(int x, int y, int maxSize)
				{
					var nodeX = x_axis[x];

					var countY = ++y_size[x];
					nodeX.InsertNode(y, maxSize, countY);
				    
					return nodeX;
				}

				insert_y(x, y + 1, length).SplitY(y);
			}

			public void SplitX(int x, int countX, int length)
			{
				var a = x_axis[x];
				var b = InsertNodeX(x + 1, countX, length);

				y_size[x + 1] = y_size[x] / 2;
				y_size[x] -= y_size[x + 1];

				a.Split(b, y_size[x], y_size[x + 1]);

				int y, volume;
				for (y = volume = 0; y < y_size[x]; y++)
				{
					volume += a.CountZ(y);
				}

				x_volume[x + 1] = x_volume[x] - volume;
				x_volume[x] = volume;

				x_floor[x + 1] = b.Floor;
			}

			public void Split(WNode target, int offset, int length)
			{
				Array.Copy(x_floor, offset, target.x_floor, 0, length);
				Array.Copy(x_axis, offset, target.x_axis, 0, length);
				Array.Copy(x_volume, offset, target.x_volume, 0, length);
				Array.Copy(y_size, offset, target.y_size, 0, length);
			}

			public void Merge(WNode source, int offset, int length, int capacity)
			{
				Resize(capacity);

				Array.Copy(source.x_floor, 0, x_floor, offset, length);
				Array.Copy(source.x_axis, 0, x_axis, offset, length);
				Array.Copy(source.x_volume, 0, x_volume, offset, length);
				Array.Copy(source.y_size, 0, y_size, offset, length);

			}

			public void MergeX(in int x1, in int x2, int capacity)
			{
				var a = x_axis[x1];
				var b = x_axis[x2];

				var length = y_size[x2];
				var offset = y_size[x1];
				a.Merge(b, offset, length, capacity);

				y_size[x1] += y_size[x2];
				x_volume[x1] += x_volume[x2];

			}

			public void RemoveX(int x, int countX)
			{
				var length = countX - x;

				Array.Copy(x_floor, x + 1, x_floor, x, length);
				Array.Copy(x_axis, x + 1, x_axis, x, length);
				Array.Copy(x_volume, x + 1, x_volume, x, length);
				Array.Copy(y_size, x + 1, y_size, x, length);
			}

			public void SetFloor(int key)
			{
				x_floor[0] = key;
				x_axis[0].SetFloor(key);

			}
		}
	}
}