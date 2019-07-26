using System;
using System.IO;

namespace Cube
{
    public partial class Cube
    {
	    private const int BSC_M = 8;
	    private const int BSC_Z_MAX = 32;
	    private const int BSC_Z_MIN = 8;

	    private int[] _w_floor;
	    private int[] _w_volume;
	    private WNode[] _w_axis;
		private int[] _x_size;
		private int _volume;
		private int _w_size;
		private int _m_size;

		public Cube()
		{
			_w_floor = new int[0];
			_w_volume = new int[0];
			_w_axis = new WNode[0];
			_x_size = new int[0];

			_volume = 0;
			_w_size = 0;
			_m_size = 0;
		}

		public struct Offset
		{
			public int W { get; }
			public int X { get; }
			public int Y { get; }
			public int Z { get; }

			public Offset(int w, int x, int y, int z)
			{
				W = w;
				X = x;
				Y = y;
				Z = z;
			}

			public void Deconstruct(out int w, out int x, out int y, out int z)
			{
				w = W;
				x = X;
				y = Y;
				z = Z;
			}
		}

		private (int, string) Value(Offset offset)
		{
			var (w, x, y, z) = offset;
			return _w_axis[w].x_axis[x].Get(y, z);
		}

		private string Value(Offset offset, string value)
		{
			var (w, x, y, z) = offset;

			var xNode = _w_axis[w].x_axis[x];
			return xNode.Set(y, z, value);
		}

		public string SetIndex(int index, string val)
		{
			return FindIndex(index, out var offset) 
				? Value(offset, val) 
				: default;
		}

		public (int, string) GetIndex(int index)
		{
			return FindIndex(index, out var offset) 
				? Value(offset)
				: default;
		}

		public string DeleteIndex(int index)
		{
			return FindIndex(index, out var offset) 
				? RemoveZ(offset)
				: default;
		}

		public string DeleteKey(int key)
		{
			var (p, _) = FindKey(key);
			return p == null 
				? null 
				: RemoveZ(p.Value);
		}

		public string GetKey(int key)
		{
			var (_, value) = FindKey(key);
			return value;
		}

		private string RemoveZ(Offset offset)
		{
			var (w, x, y, z) = offset;

			var nodeW = _w_axis[w];
			var nodeX = nodeW.x_axis[x];
			
			_volume--;
			_w_volume[w]--;
			nodeW.x_volume[x]--;

			var val = nodeX.RemoveZ(y, z);
			var countZ = nodeX.CountZ(y);

			if (countZ > 0)
			{
				if (z == 0)
				{
					nodeX.ResetFloor(y);

					if (y == 0)
					{
						nodeW.x_floor[x] = nodeX.Key(y, z);

						if (x == 0)
						{
							_w_floor[w] = nodeX.Key(y, z);
						}
					}
				}

				if (y != 0 && countZ < BSC_Z_MIN && nodeX.CountZ(y-1) < BSC_Z_MIN)
				{
					MergeY(w, x, y - 1, y);

					if (x != 0 && nodeW.y_size[x] < _m_size / 4 && nodeW.y_size[x - 1] < _m_size / 4)
					{
						MergeX(w, x - 1, x);

						if (w != 0 && _x_size[w] < _m_size / 4 && _x_size[w - 1] < _m_size / 4)
						{
							MergeW(w - 1, w);
						}
					}
				}
			}
			else
			{
				RemoveY(_w_axis[w], w, x, y);
			}

			return val;
		}
		
		private void MergeW(int w1, int w2)
		{
			var a = _w_axis[w1];
			var b = _w_axis[w2];

			a.Merge(b, _x_size[w1], _x_size[w2], _m_size);

			_x_size[w1] += _x_size[w2];
			_w_volume[w1] += _w_volume[w2];

			RemoveW(w2);
		}

		private void MergeX(int w, int x1, int x2)
		{
			var nodeW = _w_axis[w];
			nodeW.MergeX(x1, x2, _m_size);
			RemoveX(nodeW, w, x2);
		}

		private void MergeY(int w, int x, int y1, int y2)
		{
			_w_axis[w].x_axis[x].MergeY(y1, y2);
			RemoveY(_w_axis[w], w, x, y2);
		}

		private void RemoveY(WNode nodeW, int w, int x, int y)
		{
			var nodeX = nodeW.x_axis[x];

			nodeW.y_size[x]--;

			if (nodeW.y_size[x] == 0)
			{
				RemoveX(nodeW, w, x);
				return;
			}

			if (nodeW.y_size[x] != y)
			{
				nodeX.RemoveNode(y, nodeW.y_size[x]);
			}

			if (y > 0) 
				return;

			nodeW.x_floor[x] = nodeX.Floor;
			if (x == 0)
			{
				_w_floor[w] = nodeX.Floor;
			}
		}

		private void RemoveX(WNode nodeW, int w, int x)
		{
			_x_size[w]--;

			if (_x_size[w] == 0)
			{
				RemoveW(w);
				return;
			}

			if (_x_size[w] != x)
			{
				nodeW.RemoveX(x, _x_size[w]);
			}

			if (x == 0)
			{
				_w_floor[w] = nodeW.x_floor[0];
			}
		}

		private void RemoveW(int w)
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

				_w_floor[0] = key;
				_w_axis[0].SetFloor(key);

				Insert(0, 0, 0, 0, (key, value));
			}
			else if (key < _w_floor[0])
			{
				_w_floor[0] = key;
				_w_axis[0].SetFloor(key);

				Insert(0, 0, 0, 0, (key, value));
			}
			else
			{
				var (w, x, y, z) = FindKeyOffset(key, _w_size);

				if (key == _w_axis[w].x_axis[x].Key(y, z))
				{
					_w_axis[w].x_axis[x].Set(y, z, value);
					return;
				}

				++z;
				Insert(w, x, y, z, (key, value));
			}
		}

		private void Insert(int w, int x, int y, int z, (int key, string value) value)
		{
			var nodeW = _w_axis[w];
			var nodeX = nodeW.x_axis[x];

			_volume++;
			_w_volume[w]++;
			nodeW.x_volume[x]++;

			var countZ = nodeX.AddZ(y, z, value);
			if (countZ == BSC_Z_MAX)
			{
				SplitY(w, x, y);
			}
		}

		public void Validate()
		{
			if (_w_size == 0)
			{
				return ;
			}

			var last = _w_floor[0];

			for (var w = 0 ; w < _w_size ; w++)
			{
				for (var x = 0 ; x < _x_size[w] ; x++)
				{
					for (var y = 0 ; y < _w_axis[w].y_size[x] ; y++)
					{
						foreach (var key in _w_axis[w].x_axis[x].Keys(y))
						{
							if (last > key)
							{
								throw new InvalidOperationException($"Unexpected key: {key} >= {last}");
							}

							last = key;
						}
					}
				}
			}
		}

		public void Dump(StringWriter sw, int depth)
		{
			for (var w = 0 ; w < _w_size ; w++)
			{
				var nodeW = _w_axis[w];

				if (depth == 1)
				{
					sw.WriteLine($"w index [{w:d3}] x size [{_x_size[w]:d3}] volume [{_w_volume[w]:d8}]\n");
					continue;
				}

				for (var x = 0 ; x < _x_size[w] ; x++)
				{
					var nodeX = nodeW.x_axis[x];

					if (depth == 2)
					{
						sw.WriteLine($"w [{w:d3}] x [{x:d3}] s [{nodeW.y_size[x]:d3}] v [{nodeW.x_volume[x]:d8}");
						continue;
					}

					for (var y = 0 ; y < nodeW.y_size[x] ; y++)
					{
						if (depth == 3)
						{
							sw.WriteLine("w [{0:d3}] x [{1:d3}] y [{2:d3}] s [{3:d3}]\n", w, x, y, nodeX.Keys(y).Length);
							continue;
						}

						int z = 0;
						foreach (var (key, val) in nodeX.KeyValues(y))
						{
							sw.WriteLine("w [{0:d3}] x [{1:d3}] y [{2:d3}] z [{3:d3}] [{4:d8}] ({5})\n", w, x, y, z++, key, val);
						}
					}
				}
			}
		}

		private void Move(int sourceIndex, int destinationIndex, int length)
		{
			Array.Copy(_w_floor, sourceIndex, _w_floor, destinationIndex, length);
			Array.Copy(_w_axis, sourceIndex, _w_axis, destinationIndex, length);
			Array.Copy(_w_volume, sourceIndex, _w_volume, destinationIndex, length);
			Array.Copy(_x_size, sourceIndex, _x_size, destinationIndex, length);
		}

		private void Resize(int length)
		{
			Array.Resize(ref _w_floor, length);
			Array.Resize(ref _w_axis, length);
			Array.Resize(ref _w_volume, length);
			Array.Resize(ref _x_size, length);
		}
		
		private WNode InsertNode(int w)
		{
			_w_size++;

			if (_w_size == _m_size)
			{
				_m_size += BSC_M;
				Resize(_m_size);
			}

			if (w + 1 != _w_size)
			{
				Move(w, w + 1, _w_size - w - 1);
			}

			_w_axis[w] = new WNode(_m_size);
			return _w_axis[w];
		}

		private void SplitW(int w)
		{
			int x;
			int volume;

			var a = _w_axis[w];
			var b = InsertNode(w + 1);

			_x_size[w + 1] = _x_size[w] / 2;
			_x_size[w] -= _x_size[w + 1];

			a.Split(b, _x_size[w], _x_size[w + 1]);

			for (x = volume = 0 ; x < _x_size[w] ; x++)
			{
				volume += a.x_volume[x];
			}

			_w_volume[w + 1] = _w_volume[w] - volume;
			_w_volume[w] = volume;

			_w_floor[w + 1] = b.x_floor[0];
		}

		private void SplitX(int w, int x)
		{
			var countX = ++_x_size[w];
			_w_axis[w].SplitX(x, countX, _m_size);
		}

		private void SplitY(int w, int x, int y)
		{
			_w_axis[w].SplitY(x, y, _m_size);
			if (_w_axis[w].y_size[x] != _m_size) 
				return;

			SplitX(w, x);

			if (_x_size[w] == _m_size)
			{
				SplitW(w);
			}
		}

		private void InitStructs()
		{
			_m_size = BSC_M;
			_w_floor = new int[BSC_M];
			_w_axis = new WNode[BSC_M];
			_w_volume = new int[BSC_M];
			_x_size = new int[BSC_M];

			var wNode = _w_axis[0] = new WNode(BSC_M);
			var xNode = wNode.x_axis[0] = new XNode(BSC_M);

			xNode.Reset(0);

			_w_size = _x_size[0] = wNode.y_size[0] = 1;
			_volume = _w_volume[0] = wNode.x_volume[0] = 0;
		}

		private bool FindIndex(int index, out Offset offset)
		{
			if (index < 0 || index >= _volume)
			{
				offset = default;
				return false;
			}

			if (index < _volume / 2)
			{
				var total = 0;

				for (var w = 0; w < _w_size; w++)
				{
					if (_w_axis[w].FindIndexForward(index, w, _w_volume[w], _x_size[w], ref total, out var found))
					{
						offset = found ?? default;
						return found != null;
					}

					total += _w_volume[w];
				}

				offset = default;
				return false;
			}
			else
			{
				var total = _volume;

				for (var w = _w_size - 1; w >= 0; w--)
				{
					if (_w_axis[w].FindIndexBackward(index, w, _w_volume[w], _x_size[w], ref total, out var found))
					{
						offset = found ?? default;
						return found != null;
					}

					total -= _w_volume[w];
				}

				offset = default;
				return false;
			}
		}

		private (Offset? offset, string value) FindKey(int target)
	    {
		    if (_w_size == 0 || target < _w_floor[0])
		    {
			    return default;
		    }

		    var (w, x, y, z) = FindKeyOffset(target, _w_size);

		    var nodeX = _w_axis[w].x_axis[x];
		    var (key, val) = nodeX.Get(y, z);
			
		    return key == target 
			    ? (new Offset(w, x, y, z), val) 
			    : (new Offset(w, x, y, z + 1), null);
	    }

		private Offset FindKeyOffset(int key, int countW)
		{
			var w = countW - 1;
			var mid = w;

			while (mid > 3)
			{
				mid /= 2;

				if (key < _w_floor[w - mid])
				{
					w -= mid;
				}
			}

			while (key < _w_floor[w]) 
				w--;

			var (x, y, z) = _w_axis[w].FindKeyOffset(key, _x_size[w]);
			return new Offset(w, x, y, z);
		}
    }
}
