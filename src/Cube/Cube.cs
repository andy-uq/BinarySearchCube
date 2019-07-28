using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cube
{
    public partial class Cube
    {
	    private const int CapacityGrowthRate = 8;
	    private const int MaxCountZ = 32;
	    private const int MinCountZ = 8;

	    private WNode[] _axisW;

	    private int[] _floor;
	    private int[] _volumeW;
		private int[] _countX;
		private int _volume;
		private int _countW;
		private int _capacity;

		public Cube()
		{
			_floor = new int[0];
			_volumeW = new int[0];
			_axisW = new WNode[0];
			_countX = new int[0];

			_volume = 0;
			_countW = 0;
			_capacity = 0;
		}

		private struct Offset
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

		public IEnumerable<int> Keys() => Enumerable.Range(0, _countW).SelectMany(Keys);
		private IEnumerable<int> Keys(int w) => Enumerable.Range(0, _countX[w]).SelectMany(x => _axisW[w].Keys(x));

		public IEnumerable<string> Values() => Enumerable.Range(0, _countW).SelectMany(Values);
		private IEnumerable<string> Values(int w) => Enumerable.Range(0, _countX[w]).SelectMany(x => _axisW[w].Values(x));

		public IEnumerable<(int key, string value)> KeyValues() => Enumerable.Range(0, _countW).SelectMany(KeyValues);
		private IEnumerable<(int key, string value)> KeyValues(int w) => Enumerable.Range(0, _countX[w]).SelectMany(x => _axisW[w].KeyValues(x));

		private (int, string) Value(Offset offset)
		{
			var (w, x, y, z) = offset;
			return _axisW[w].Get(x, y, z);
		}

		private string Value(Offset offset, string value)
		{
			var (w, x, y, z) = offset;
			return _axisW[w].Set(x, y, z, value);
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

		public void SetKey(int key, string value)
		{
			if (_countW == 0)
			{
				InitStructs();

				_floor[0] = key;
				_axisW[0].SetFloor(key);

				Insert(0, 0, 0, 0, (key, value));
			}
			else if (key < _floor[0])
			{
				_floor[0] = key;
				_axisW[0].SetFloor(key);

				Insert(0, 0, 0, 0, (key, value));
			}
			else
			{
				var (w, x, y, z) = FindKeyOffset(key, _countW);

				if (key == _axisW[w].Key(x, y, z))
				{
					_axisW[w].Set(x, y, z, value);
					return;
				}

				++z;
				Insert(w, x, y, z, (key, value));
			}
		}

		public void Validate()
		{
			if (_countW == 0)
			{
				return ;
			}

			var last = _floor[0];

			for (var w = 0 ; w < _countW ; w++)
			{
				for (var x = 0 ; x < _countX[w] ; x++)
				{
					foreach (var key in _axisW[w].Keys(x))
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

		public void Dump(StringWriter sw, int depth)
		{
			for (var w = 0 ; w < _countW ; w++)
			{
				var nodeW = _axisW[w];

				if (depth == 1)
				{
					sw.WriteLine($"w index [{w:d3}] x size [{_countX[w]:d3}] volume [{_volumeW[w]:d8}]\n");
					continue;
				}

				for (var x = 0 ; x < _countX[w] ; x++)
				{
					if (depth == 2)
					{
						sw.WriteLine($"w [{w:d3}] x [{x:d3}] s [{nodeW.CountY(x):d3}] v [{nodeW.VolumeX(x):d8}");
						continue;
					}

					for (var y = 0 ; y < nodeW.CountY(x); y++)
					{
						if (depth == 3)
						{
							sw.WriteLine("w [{0:d3}] x [{1:d3}] y [{2:d3}] s [{3:d3}]\n", w, x, y, nodeW.Keys(x, y).Count());
							continue;
						}

						int z = 0;
						foreach (var (key, val) in nodeW.KeyValues(x, y))
						{
							sw.WriteLine("w [{0:d3}] x [{1:d3}] y [{2:d3}] z [{3:d3}] [{4:d8}] ({5})\n", w, x, y, z++, key, val);
						}
					}
				}
			}
		}

		private string RemoveZ(Offset offset)
		{
			var (w, x, y, z) = offset;

			var nodeW = _axisW[w];
			
			_volume--;
			_volumeW[w]--;

			var val = nodeW.RemoveZ(x, y, z);
			var countZ = nodeW.CountZ(x, y);

			if (countZ > 0)
			{
				if (z == 0)
				{
					nodeW.ResetFloor(x, y);

					if (y == 0)
					{
						nodeW.ResetFloor(x);

						if (x == 0)
						{
							_floor[w] = nodeW.Floor;
						}
					}
				}

				if (y == 0 || countZ >= MinCountZ || nodeW.CountZ(x, y - 1) >= MinCountZ)
				{
					return val;
				}

				MergeY(w, x, y - 1, y);

				if (x == 0 || nodeW.CountY(x) >= _capacity / 4 || nodeW.CountY(x - 1) >= _capacity / 4)
				{
					return val;
				}

				MergeX(w, x - 1, x);

				if (w == 0 || _countX[w] >= _capacity / 4 || _countX[w - 1] >= _capacity / 4)
				{
					return val;
				}

				MergeW(w - 1, w);
			}
			else
			{
				RemoveY(_axisW[w], w, x, y);
			}

			return val;
		}
		
		private void MergeW(int w1, int w2)
		{
			var a = _axisW[w1];
			var b = _axisW[w2];

			a.MergeW(b, _countX[w1], _countX[w2], _capacity);

			_countX[w1] += _countX[w2];
			_volumeW[w1] += _volumeW[w2];

			RemoveW(w2);
		}

		private void MergeX(int w, int x1, int x2)
		{
			var nodeW = _axisW[w];
			nodeW.MergeX(x1, x2, _capacity);
			RemoveX(nodeW, w, x2);
		}

		private void MergeY(int w, int x, int y1, int y2)
		{
			_axisW[w].MergeY(x, y1, y2);
			RemoveY(_axisW[w], w, x, y2);
		}

		private void RemoveY(WNode nodeW, int w, int x, int y)
		{
			var countY = nodeW.RemoveY(x);

			if (countY == 0)
			{
				RemoveX(nodeW, w, x);
				return;
			}

			if (countY != y)
			{
				nodeW.RemoveNode(x, y, countY);
			}

			if (y > 0) 
				return;

			var floor = nodeW.ResetFloor(x);
			if (x == 0)
			{
				_floor[w] = floor;
			}
		}

		private void RemoveX(WNode nodeW, int w, int x)
		{
			_countX[w]--;

			if (_countX[w] == 0)
			{
				RemoveW(w);
				return;
			}

			if (_countX[w] != x)
			{
				nodeW.RemoveX(x, _countX[w]);
			}

			if (x == 0)
			{
				_floor[w] = nodeW.Floor;
			}
		}

		private void RemoveW(int w)
		{
			_countW--;

			if (_countW < _capacity - CapacityGrowthRate)
			{
				_capacity -= CapacityGrowthRate;
			}

			if (_countW == 0 || _countW == w) 
				return;

			var length = _countW - w;
			Array.Copy(_floor, w + 1, _floor, w, length);
			Array.Copy(_axisW, w + 1, _axisW, w, length);
			Array.Copy(_volumeW, w + 1, _volumeW, w, length);
			Array.Copy(_countX, w + 1, _countX, w, length);
		}

		private void Insert(int w, int x, int y, int z, (int key, string value) value)
		{
			var nodeW = _axisW[w];

			_volume++;
			_volumeW[w]++;

			var countZ = nodeW.AddZ(x, y, z, value);
			if (countZ == MaxCountZ)
			{
				SplitY(w, x, y);
			}
		}

		private void Move(int sourceIndex, int destinationIndex, int length)
		{
			Array.Copy(_floor, sourceIndex, _floor, destinationIndex, length);
			Array.Copy(_axisW, sourceIndex, _axisW, destinationIndex, length);
			Array.Copy(_volumeW, sourceIndex, _volumeW, destinationIndex, length);
			Array.Copy(_countX, sourceIndex, _countX, destinationIndex, length);
		}

		private void Resize(int length)
		{
			Array.Resize(ref _floor, length);
			Array.Resize(ref _axisW, length);
			Array.Resize(ref _volumeW, length);
			Array.Resize(ref _countX, length);
		}
		
		private WNode InsertW(int w)
		{
			_countW++;

			if (_countW == _capacity)
			{
				_capacity += CapacityGrowthRate;
				Resize(_capacity);
			}

			if (w + 1 != _countW)
			{
				Move(w, w + 1, _countW - w - 1);
			}

			_axisW[w] = new WNode(_capacity);
			return _axisW[w];
		}

		private void SplitW(int w)
		{
			int x;
			int volume;

			var a = _axisW[w];
			var b = InsertW(w + 1);

			_countX[w + 1] = _countX[w] / 2;
			_countX[w] -= _countX[w + 1];

			a.SplitW(b, _countX[w], _countX[w + 1]);

			for (x = volume = 0 ; x < _countX[w] ; x++)
			{
				volume += a.VolumeX(x);
			}

			_volumeW[w + 1] = _volumeW[w] - volume;
			_volumeW[w] = volume;

			_floor[w + 1] = b.Floor;
		}

		private void SplitX(int w, int x)
		{
			var countX = ++_countX[w];
			_axisW[w].SplitX(x, countX, _capacity);
		}

		private void SplitY(int w, int x, int y)
		{
			_axisW[w].SplitY(x, y, _capacity);
			if (_axisW[w].CountY(x) != _capacity) 
				return;

			SplitX(w, x);

			if (_countX[w] == _capacity)
			{
				SplitW(w);
			}
		}

		private void InitStructs()
		{
			_capacity = CapacityGrowthRate;
			_floor = new int[CapacityGrowthRate];
			_axisW = new WNode[CapacityGrowthRate];
			_volumeW = new int[CapacityGrowthRate];
			_countX = new int[CapacityGrowthRate];

			var wNode = _axisW[0] = new WNode(CapacityGrowthRate);
			wNode.ResetW();

			_countW = _countX[0] = 1;
			_volume = _volumeW[0] = 0;
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

				for (var w = 0; w < _countW; w++)
				{
					if (_axisW[w].FindIndexForwardInW(index, w, _volumeW[w], _countX[w], ref total, out var found))
					{
						offset = found ?? default;
						return found != null;
					}

					total += _volumeW[w];
				}

				offset = default;
				return false;
			}
			else
			{
				var total = _volume;

				for (var w = _countW - 1; w >= 0; w--)
				{
					if (_axisW[w].FindIndexBackwardInW(index, w, _volumeW[w], _countX[w], ref total, out var found))
					{
						offset = found ?? default;
						return found != null;
					}

					total -= _volumeW[w];
				}

				offset = default;
				return false;
			}
		}

		private (Offset? offset, string value) FindKey(int target)
	    {
		    if (_countW == 0 || target < _floor[0])
		    {
			    return default;
		    }

		    var (w, x, y, z) = FindKeyOffset(target, _countW);
		    var (key, val) = _axisW[w].Get(x, y, z);
			
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

				if (key < _floor[w - mid])
				{
					w -= mid;
				}
			}

			while (key < _floor[w]) 
				w--;

			var (x, y, z) = _axisW[w].FindKeyOffset(key, _countX[w]);
			return new Offset(w, x, y, z);
		}
    }
}
