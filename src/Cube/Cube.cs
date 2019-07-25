using System;
using System.IO;
using System.Net.Http.Headers;

namespace Cube
{
    public class Cube
    {
	    private int[] _w_floor;
	    private int[] _w_volume;
	    private WNode[] _w_axis;
		private short[] _x_size;
		private int _volume;
		private short _w_size;
		private short _m_size;

		public Cube()
		{
			_w_floor = new int[0];
			_w_volume = new int[0];
			_w_axis = new WNode[0];
			_x_size = new short[0];
			_volume = 0;
			_w_size = 0;
			_m_size = 0;
		}

		private struct Offset
		{
			public short W { get; }
			public short X { get; }
			public short Y { get; }
			public short Z { get; }

			public Offset(short w, short x, short y, short z)
			{
				W = w;
				X = x;
				Y = y;
				Z = z;
			}

			public void Deconstruct(out short w, out short x, out short y, out short z)
			{
				w = W;
				x = X;
				y = Y;
				z = Z;
			}
		}

		private (int, string) Value(Offset offset)
		{
			var yNode = _w_axis[offset.W].x_axis[offset.X].y_axis[offset.Y];
			
			var key = yNode.z_keys[offset.Z];
			var val = yNode.z_vals[offset.Z];
			
			return (key, val);
		}

		private string Value(Offset offset, string value)
		{
			var yNode = _w_axis[offset.W].x_axis[offset.X].y_axis[offset.Y];
			var previous = yNode.z_vals[offset.Z];
			yNode.z_vals[offset.Z] = value;
			return previous;
		}

		public void SetIndex(int index, string val)
		{
			var p = FindIndex(index);
			if (p != null)
			{
				Value(p.Value, val);
			}
		}

		public (int, string) GetIndex(int index)
		{
			var p = FindIndex(index);
			return p == null 
				? default 
				: Value(p.Value);
		}

		public string DeleteIndex(int index)
		{
			var p = FindIndex(index);
			return p == null 
				? null 
				: remove_z_node(p.Value);
		}

		public string DeleteKey(int key)
		{
			var (p, _) = FindKey(key);
			return p == null 
				? null 
				: remove_z_node(p.Value);
		}

		private string remove_z_node(Offset offset)
		{
			var (w, x, y, z) = offset;

			WNode w_node = _w_axis[w];
			XNode x_node = w_node.x_axis[x];
			YNode y_node = x_node.y_axis[y];
			
			_volume--;
			_w_volume[w]--;
			w_node.x_volume[x]--;

			x_node.z_size[y]--;

			var val = y_node.z_vals[z];

			if (x_node.z_size[y] != z)
			{
				var length = x_node.z_size[y] - z;
				Array.Copy(y_node.z_keys, z + 1, y_node.z_keys, z, length);
				Array.Copy(y_node.z_vals, z + 1, y_node.z_vals, z, length);
			}

			if (x_node.z_size[y] > 0)
			{
				if (z == 0)
				{
					x_node.y_floor[y] = y_node.z_keys[z];

					if (y == 0)
					{
						w_node.x_floor[x] = y_node.z_keys[z];

						if (x == 0)
						{
							_w_floor[w] = y_node.z_keys[z];
						}
					}
				}

				if (y != 0 && x_node.z_size[y] < YNode.BSC_Z_MIN && x_node.z_size[y - 1] < YNode.BSC_Z_MIN)
				{
					merge_y_node(w, x, (short) (y - 1), y);

					if (x != 0 && w_node.y_size[x] < _m_size / 4 && w_node.y_size[x - 1] < _m_size / 4)
					{
						merge_x_node(w, (short) (x - 1), x);

						if (w != 0 && _x_size[w] < _m_size / 4 && _x_size[w - 1] < _m_size / 4)
						{
							merge_w_node((short) (w - 1), w);
						}
					}
				}
			}
			else
			{
				remove_y_node(w, x, y);
			}
			return val;
		}
		
		void merge_w_node(short w1, short w2)
		{
			var w_node1 = _w_axis[w1];
			var w_node2 = _w_axis[w2];

			Array.Resize(ref w_node1.x_floor, _m_size);
			Array.Resize(ref w_node1.x_axis, _m_size);
			Array.Resize(ref w_node1.x_volume, _m_size);
			Array.Resize(ref w_node1.y_size, _m_size);

			int length = _x_size[w2];
			Array.Copy(w_node2.x_floor, 0, w_node1.x_floor, _x_size[w1], length);
			Array.Copy(w_node2.x_axis, 0, w_node1.x_axis, _x_size[w1], length);
			Array.Copy(w_node2.x_volume, 0, w_node1.x_volume, _x_size[w1], length);
			Array.Copy(w_node2.y_size, 0, w_node1.y_size, _x_size[w1], length);

			_x_size[w1] += _x_size[w2];
			_w_volume[w1] += _w_volume[w2];

			remove_w_node(w2);
		}

		private void merge_y_node(short w, short x, short y1, short y2)
		{
			var x_node = _w_axis[w].x_axis[x];
			var y_node1 = x_node.y_axis[y1];
			var y_node2 = x_node.y_axis[y2];

			var length = x_node.z_size[y2];
			Array.Copy(y_node2.z_keys, 0, y_node1.z_keys, x_node.z_size[y1], length);
			Array.Copy(y_node2.z_vals, 0, y_node1.z_vals, x_node.z_size[y1], length);

			x_node.z_size[y1] += length;

			remove_y_node(w, x, y2);
		}

		void merge_x_node(short w, short x1, short x2)
		{
			var w_node = _w_axis[w];
			var x_node1 = w_node.x_axis[x1];
			var x_node2 = w_node.x_axis[x2];

			Array.Resize(ref x_node1.y_floor, _m_size);
			Array.Resize(ref x_node1.y_axis, _m_size);
			Array.Resize(ref x_node1.z_size, _m_size);

			var length = w_node.y_size[x2];
			Array.Copy(x_node2.y_floor, 0, x_node1.y_floor, w_node.y_size[x1], length);
			Array.Copy(x_node2.y_axis, 0, x_node1.y_axis, w_node.y_size[x1], length);
			Array.Copy(x_node2.z_size, 0, x_node1.z_size, w_node.y_size[x1], length);

			w_node.y_size[x1] += w_node.y_size[x2];

			w_node.x_volume[x1] += w_node.x_volume[x2];

			remove_x_node(w, x2);
		}

		private void remove_y_node(short w, short x, short y)
		{
			WNode w_node = _w_axis[w];
			XNode x_node = w_node.x_axis[x];

			w_node.y_size[x]--;

			if (w_node.y_size[x] != 0)
			{
				if (w_node.y_size[x] != y)
				{
					int length = (w_node.y_size[x] - y);
					Array.Copy(x_node.y_floor, y + 1, x_node.y_floor, y, length);
					Array.Copy(x_node.y_axis, y + 1, x_node.y_axis, y, length);
					Array.Copy(x_node.z_size, y + 1, x_node.z_size, y, length);
				}

				if (y == 0)
				{
					_w_axis[w].x_floor[x] = x_node.y_floor[0];
					if (x == 0)
					{
						_w_floor[w] = x_node.y_floor[0];
					}
				}
			}
			else
			{
				remove_x_node(w, x);
			}
		}

		private void remove_x_node(short w, short x)
		{
			WNode w_node = _w_axis[w];

			_x_size[w]--;

			
			if (_x_size[w] != 0)
			{
				if (_x_size[w] != x)
				{
					var length = (_x_size[w] - x );
					Array.Copy(w_node.x_floor, x + 1, w_node.x_floor, x, length);
					Array.Copy(w_node.x_axis, x + 1, w_node.x_axis, x, length);
					Array.Copy(w_node.x_volume, x + 1, w_node.x_volume, x, length);
					Array.Copy(w_node.y_size, x + 1, w_node.y_size, x, length);
				}

				if (x == 0)
				{
					_w_floor[w] = w_node.x_floor[0];
				}
			}
			else
			{
				remove_w_node(w);
			}
		}

		private void remove_w_node(short w)
		{
			_w_size--;

			if (_w_size < _m_size - BSC_M)
			{
				_m_size -= BSC_M;
			}

			if (_w_size == 0 || _w_size == w) 
				return;

			var length = _w_size - w;
			Array.Copy(_w_floor, w + 1, _w_floor, w, length);
			Array.Copy(_w_axis, w + 1, _w_axis, w, length);
			Array.Copy(_w_volume, w + 1, _w_volume, w, length);
			Array.Copy(_x_size, w + 1, _x_size, w, length);
		}

		public void SetKey(int key, string value)
		{
			if (_w_size == 0)
			{
				InitStructs();
				
				var w_node = _w_axis[0];
				var x_node = w_node.x_axis[0];
				var y_node = x_node.y_axis[0];

				_w_floor[0] = w_node.x_floor[0] = x_node.y_floor[0] = key;
				insert(key, value, 0, 0, 0, 0, w_node, x_node, y_node);
			}
			else if (key < _w_floor[0])
			{
				var w_node = _w_axis[0];
				var x_node = w_node.x_axis[0];
				var y_node = x_node.y_axis[0];

				_w_floor[0] = w_node.x_floor[0] = x_node.y_floor[0] = key;
				insert(key, value, 0, 0, 0, 0, w_node, x_node, y_node);
			}
			else
			{
				short w, x, y, z;

				// w

				var mid = w = (short) (_w_size - 1);

				while (mid > 3)
				{
					mid /= 2;
					if (key < _w_floor[w - mid]) 
						w -= mid;
				}
				while (key < _w_floor[w]) --w;

				var w_node = _w_axis[w];

				// x

				mid = x = (short) (_x_size[w] - 1);

				while (mid > 3)
				{
					mid /= 2;
					if (key < w_node.x_floor[x - mid]) 
						x -= mid;
				}
				while (key < w_node.x_floor[x]) --x;

				var x_node = w_node.x_axis[x];

				// y

				mid = y = (short) (w_node.y_size[x] - 1);

				while (mid > 7)
				{
					mid /= 4;

					if (key < x_node.y_floor[y - mid])
					{
						y -= mid;
						if (key < x_node.y_floor[y - mid])
						{
							y -= mid;
							if (key < x_node.y_floor[y - mid])
							{
								y -= mid;
							}
						}
					}
				}
				while (key < x_node.y_floor[y]) --y;

				var y_node = x_node.y_axis[y];

				// z

				mid = z = (short) (x_node.z_size[y] - 1);

				while (mid > 7)
				{
					mid /= 4;

					if (key < y_node.z_keys[z - mid])
					{
						z -= mid;
						if (key < y_node.z_keys[z - mid])
						{
							z -= mid;
							if (key < y_node.z_keys[z - mid])
							{
								z -= mid;
							}
						}
					}
				}
				while (key < y_node.z_keys[z]) --z;

				if (key == y_node.z_keys[z])
				{
					y_node.z_vals[z] = value;
					return;
				}

				++z;

				insert(key, value, w, x, y, z, w_node, x_node, y_node);
			}
		}

		private void insert(int key, string value, short w, short x, short y, short z, WNode w_node, XNode x_node, YNode y_node)
		{
			_volume++;
			_w_volume[w]++;
			w_node.x_volume[x]++;
			x_node.z_size[y]++;

			if (z + 1 != x_node.z_size[y])
			{
				var length = x_node.z_size[y] - z - 1;
				Array.Copy(y_node.z_keys, z, y_node.z_keys, z + 1, length);
				Array.Copy(y_node.z_vals, z, y_node.z_vals, z + 1, length);
			}

			y_node.z_keys[z] = key;
			y_node.z_vals[z] = value;

			if (x_node.z_size[y] == YNode.BSC_Z_MAX)
			{
				split_y_node(w, x, y);

				if (_w_axis[w].y_size[x] == _m_size)
				{
					split_x_node(w, x);

					if (_x_size[w] == _m_size)
					{
						split_w_node(w);
					}
				}
			}
		}

		public void Validate()
		{
			YNode y_node;
			short w, x, y, z;
			int last;

			if (_w_size == 0)
			{
				return ;
			}

			last = _w_floor[0];

			for (w = 0 ; w < _w_size ; w++)
			{
				for (x = 0 ; x < _x_size[w] ; x++)
				{
					for (y = 0 ; y < _w_axis[w].y_size[x] ; y++)
					{
						for (z = 0 ; z < _w_axis[w].x_axis[x].z_size[y] ; z++)
						{
							y_node = _w_axis[w].x_axis[x].y_axis[y];

							if (last > y_node.z_keys[z])
							{
								throw new InvalidOperationException($"Unexpected key: {y_node.z_keys[z]} >= {last}");
							}

							last = y_node.z_keys[z];
						}
					}
				}
			}
		}

		public void Dump(StringWriter sw, short depth)
		{
			for (var w = 0 ; w < _w_size ; w++)
			{
				var w_node = _w_axis[w];

				if (depth == 1)
				{
					sw.WriteLine($"w index [{w:d3}] x size [{_x_size[w]:d3}] volume [{_w_volume[w]:d8}]\n");
					continue;
				}

				for (var x = 0 ; x < _x_size[w] ; x++)
				{
					var x_node = w_node.x_axis[x];

					if (depth == 2)
					{
						sw.WriteLine($"w [{w:d3}] x [{x:d3}] s [{w_node.y_size[x]:d3}] v [{w_node.x_volume[x]:d8}");
						continue;
					}

					for (var y = 0 ; y < w_node.y_size[x] ; y++)
					{
						var y_node = x_node.y_axis[y];

						if (depth == 3)
						{
							sw.WriteLine("w [{0:d3}] x [{1:d3}] y [{2:d3}] s [{3:d3}]\n", w, x, y, x_node.z_size[y]);
							continue;
						}

						for (var z = 0 ; z < x_node.z_size[y] ; z++)
						{
							sw.WriteLine("w [{0:d3}] x [{1:d3}] y [{2:d3}] z [{3:d3}] [{4:d8}] ({5})\n", w, x, y, z, y_node.z_keys[z], y_node.z_vals[z]);
						}
					}
				}
			}
		}

		void insert_w_node(short w)
		{
			WNode w_node;

			++_w_size;

			if (_w_size == _m_size)
			{
				_m_size += BSC_M;

				Array.Resize(ref _w_floor, _m_size);
				Array.Resize(ref _w_axis, _m_size);
				Array.Resize(ref _w_volume, _m_size);
				Array.Resize(ref _x_size, _m_size);
			}

			if (w + 1 != _w_size)
			{
				var length = _w_size - w - 1;
				Array.Copy(_w_floor, w, _w_floor, w + 1, length);
				Array.Copy(_w_axis, w, _w_axis, w + 1, length);
				Array.Copy(_w_volume, w, _w_volume, w + 1, length);
				Array.Copy(_x_size, w, _x_size, w + 1, length);
			}

			w_node = _w_axis[w] = new WNode();

			w_node.x_floor = new int[_m_size];
			w_node.x_axis = new XNode[_m_size];
			w_node.y_size = new short[_m_size];
			w_node.x_volume = new int[_m_size];
		}

		private void split_w_node(short w)
		{
			WNode w_node1, w_node2;
			int x;
			int volume;

			insert_w_node((short) (w + 1));

			w_node1 = _w_axis[w];
			w_node2 = _w_axis[w + 1];

			_x_size[w + 1] = (short) (_x_size[w] / 2);
			_x_size[w] -= _x_size[w + 1];

			int length = _x_size[w + 1];
			Array.Copy(w_node1.x_floor, _x_size[w], w_node2.x_floor, 0, length);
			Array.Copy(w_node1.x_axis, _x_size[w], w_node2.x_axis, 0, length);
			Array.Copy(w_node1.x_volume, _x_size[w], w_node2.x_volume, 0, length);
			Array.Copy(w_node1.y_size, _x_size[w], w_node2.y_size, 0, length);

			for (x = volume = 0 ; x < _x_size[w] ; x++)
			{
				volume += w_node1.x_volume[x];
			}

			_w_volume[w + 1] = _w_volume[w] - volume;
			_w_volume[w] = volume;

			_w_floor[w + 1] = w_node2.x_floor[0];
		}

		void insert_x_node(short w, short x)
		{
			WNode w_node = _w_axis[w];

			short x_size = ++_x_size[w];

			if (x_size % BSC_M == 0 && x_size < _m_size)
			{
			   Array.Resize(ref w_node.x_floor, _m_size);
			   Array.Resize(ref w_node.x_axis, _m_size);
			   Array.Resize(ref w_node.x_volume, _m_size);
			   Array.Resize(ref w_node.y_size, _m_size);
			}

			if (x_size != x + 1)
			{
				var length = x_size - x - 1;
				Array.Copy(w_node.x_floor, x, w_node.x_floor, x + 1, length);
				Array.Copy(w_node.x_axis, x, w_node.x_axis, x + 1, length);
				Array.Copy(w_node.x_volume, x, w_node.x_volume, x + 1, length);
				Array.Copy(w_node.y_size, x, w_node.y_size, x + 1, length);
			}

			var x_node = w_node.x_axis[x] = new XNode();

			x_node.y_floor = new int[_m_size];
			x_node.y_axis = new YNode[_m_size];
			x_node.z_size = new short[_m_size];
		}

		private void split_x_node(short w, short x)
		{
			insert_x_node(w, (short) (x + 1));

			var w_node = _w_axis[w];

			var x_node1 = w_node.x_axis[x];
			var x_node2 = w_node.x_axis[x + 1];

			w_node.y_size[x + 1] = (short) (w_node.y_size[x] / 2);
			w_node.y_size[x] -= w_node.y_size[x + 1];

			var length = w_node.y_size[x + 1];
			Array.Copy(x_node1.y_floor, w_node.y_size[x], x_node2.y_floor, 0, length);
			Array.Copy(x_node1.y_axis, w_node.y_size[x], x_node2.y_axis, 0, length);
			Array.Copy(x_node1.z_size, w_node.y_size[x], x_node2.z_size, 0, length);

			int y;
			int volume;
			for (y = volume = 0; y < w_node.y_size[x] ; y++)
			{
				volume += x_node1.z_size[y];
			}

			w_node.x_volume[x + 1] = w_node.x_volume[x] - volume;
			w_node.x_volume[x] = volume;

			_w_axis[w].x_floor[x + 1] = x_node2.y_floor[0];
		}

		private void split_y_node(short w, short x, short y)
		{
			insert_y_node(w, x, (short) (y + 1));

			var x_node = _w_axis[w].x_axis[x];

			var y_node1 = x_node.y_axis[y];
			var y_node2 = x_node.y_axis[y + 1];

			x_node.z_size[y + 1] = (short) (x_node.z_size[y] / 2);
			x_node.z_size[y] -= x_node.z_size[y + 1];

			Array.Copy(y_node1.z_keys, x_node.z_size[y], y_node2.z_keys, 0,  x_node.z_size[y + 1]);
			Array.Copy(y_node1.z_vals, x_node.z_size[y], y_node2.z_vals, 0,  x_node.z_size[y + 1]);

			x_node.y_floor[y + 1] = y_node2.z_keys[0];
		}

		void insert_y_node(short w, short x, short y)
		{
			XNode x_node = _w_axis[w].x_axis[x];

			short y_size = ++_w_axis[w].y_size[x];

			if (y_size % BSC_M == 0 && y_size < _m_size)
			{
				Array.Resize(ref x_node.y_floor, _m_size);
				Array.Resize(ref x_node.y_axis, _m_size);
				Array.Resize(ref x_node.z_size, _m_size);
			}

			var destinationIndex = y + 1;
			if (y_size != destinationIndex)
			{
				var length = y_size - y - 1;
				Array.Copy(x_node.y_floor, y, x_node.y_floor, destinationIndex, length);
				Array.Copy(x_node.y_axis, y, x_node.y_axis, destinationIndex, length);
				Array.Copy(x_node.z_size, y, x_node.z_size, destinationIndex, length);
			}

			x_node.y_axis[y] = new YNode();
		}

		public const int BSC_M = 8;

		private void InitStructs()
		{
			_m_size = BSC_M;
			_w_floor = new int[BSC_M];
			_w_axis = new WNode[BSC_M];
			_w_volume = new int[BSC_M];
			_x_size = new short[BSC_M];

			var wNode = _w_axis[0] = new WNode();
			wNode.x_floor = new int[BSC_M];
			wNode.x_axis = new XNode[BSC_M];
			wNode.y_size = new short[BSC_M];
			wNode.x_volume = new int[BSC_M];

			var xNode = wNode.x_axis[0] = new XNode();
			xNode.y_floor = new int[BSC_M];
			xNode.y_axis = new YNode[BSC_M];
			xNode.z_size = new short[BSC_M];

			var yNode = xNode.y_axis[0] = new YNode();
			xNode.z_size[0] = 0;

			_w_size = _x_size[0] = wNode.y_size[0] = 1;
			_volume = _w_volume[0] = wNode.x_volume[0] = 0;
		}

		private Offset? FindIndex(int index)
		{
			if (index < 0 || index >= _volume)
			{
				return default;
			}

			return index < _volume / 2 
				? forward_w(index) 
				: backward_w(index);
		}

		private Offset? forward_w(int index)
		{
			var total = 0;

			for (short w = 0; w < _w_size; w++)
			{
				var w_node = _w_axis[w];

				if (total + _w_volume[w] > index)
				{
					if (index > total + _w_volume[w] / 2)
					{
						total += _w_volume[w];
						return backward_x(index, w, w_node, ref total);
					}

					var found = forward_x(index, w, w_node, ref total);
					if (found != null)
					{
						return found;
					}
				}

				total += _w_volume[w];
			}

			return default;
		}

		private Offset? backward_w(int index)
		{
			var total = _volume;

			for (var w = (short)(_w_size - 1); w >= 0; w--)
			{
				var w_node = _w_axis[w];

				if (total - _w_volume[w] <= index)
				{
					if (index < total - _w_volume[w] / 2)
					{
						total -= _w_volume[w];
						return forward_x(index, w, w_node, ref total);
					}

					var found = backward_x(index, w, w_node, ref total);
					if (found != null)
					{
						return found;
					}
				}

				total -= _w_volume[w];
			}

			return default;
		}

		private Offset? forward_x(int index, short w, WNode w_node, ref int total)
		{
			for (short x = 0; x < _x_size[w]; x++)
			{
				var x_node = w_node.x_axis[x];

				if (total + w_node.x_volume[x] > index)
				{
					if (index > total + w_node.x_volume[x] / 2)
					{
						total += w_node.x_volume[x];
						return backward_y(index, w, w_node, x, x_node, ref total);
					}

					var found = forward_y(index, w, w_node, x, x_node, ref total);
					if (found != null)
					{
						return found;
					}
				}

				total += w_node.x_volume[x];
			}

			return default;
		}

		private Offset? backward_x(int index, short w, WNode w_node, ref int total)
		{
			for (short x = (short)(_x_size[w] - 1); x >= 0; x--)
			{
				var x_node = w_node.x_axis[x];

				if (total - w_node.x_volume[x] <= index)
				{
					if (index < total - w_node.x_volume[x] / 2)
					{
						total -= w_node.x_volume[x];
						return forward_y(index, w, w_node, x, x_node, ref total);
					}

					var found = backward_y(index, w, w_node, x, x_node, ref total);
					if (found != null)
					{
						return found;
					}
				}

				total -= w_node.x_volume[x];
			}

			return default;
		}

		private static Offset? forward_y(int index, short w, WNode w_node, short x, XNode x_node, ref int total)
		{
			for (short y = 0; y < w_node.y_size[x]; y++)
			{
				if (total + x_node.z_size[y] > index)
				{
					var z = (short)(index - total);
					return new Offset(w, x, y, z);
				}

				total += x_node.z_size[y];
			}

			return default;
		}

		private static Offset? backward_y(int index, short w, WNode w_node, short x, XNode x_node, ref int total)
		{
			for (var y = (w_node.y_size[x] - 1); y >= 0; y--)
			{
				if (total - x_node.z_size[y] <= index)
				{
					var z = (short) (x_node.z_size[y] - (total - index));
					return new Offset(w, x, (short) y, z);
				}

				total -= x_node.z_size[y];
			}

			return default;
		}

		private (Offset? offset, string value) FindKey(int key)
	    {
		    short mid, w, x, y, z;

		    if (_w_size == 0 || key < _w_floor[0])
		    {
			    return default;
		    }

		    // w

		    mid = w = (short)(_w_size - 1);

		    while (mid > 3)
		    {
			    mid /= 2;

			    if (key < _w_floor[w - mid]) w -= mid;
		    }
		    while (key < _w_floor[w]) --w;

		    var w_node = _w_axis[w];

		    // x

		    mid = x = (short)(_x_size[w] - 1);

		    while (mid > 3)
		    {
			    mid /= 2;

			    if (key < w_node.x_floor[x - mid]) x -= mid;
		    }
		    while (key < w_node.x_floor[x]) --x;

		    var x_node = w_node.x_axis[x];

		    // y

		    mid = y = (short)(w_node.y_size[x] - 1);

		    while (mid > 7)
		    {
			    mid /= 4;

			    if (key < x_node.y_floor[y - mid])
			    {
				    y -= mid;
				    if (key < x_node.y_floor[y - mid])
				    {
					    y -= mid;
					    if (key < x_node.y_floor[y - mid])
					    {
						    y -= mid;
					    }
				    }
			    }
		    }
		    while (key < x_node.y_floor[y]) --y;

		    var y_node = x_node.y_axis[y];

		    // z

		    mid = z = (short)(x_node.z_size[y] - 1);

		    while (mid > 7)
		    {
			    mid /= 4;

			    if (key < y_node.z_keys[z - mid])
			    {
				    z -= mid;
				    if (key < y_node.z_keys[z - mid])
				    {
					    z -= mid;
					    if (key < y_node.z_keys[z - mid])
					    {
						    z -= mid;
					    }
				    }
			    }
		    }
		    while (key < y_node.z_keys[z]) --z;

		    if (key == y_node.z_keys[z])
		    {
			    return (new Offset(w, x, y, z), y_node.z_vals[z]);
		    }

		    return (new Offset(w, x, y, (short)(z + 1)), null);
	    }

	    public class WNode
	    {
		    public XNode[] x_axis;
		    public int[] x_volume;
		    public short[] y_size;
		    public int[] x_floor;
	    }

	    public class XNode
	    {
		    public YNode[] y_axis;
		    public short[] z_size;
		    public int[] y_floor;
	    }

	    public class YNode
	    {
		    public const int BSC_Z_MAX = 32;
		    public const int BSC_Z_MIN = 8;

			public int[] z_keys;
		    public string[] z_vals;

		    public YNode()
		    {
			    z_keys = new int[BSC_Z_MAX];
				z_vals = new string[BSC_Z_MAX];
		    }
	    }

	    public string GetKey(int key)
	    {
		    var p = FindKey(key);
		    return p.value;
	    }
    }
}
