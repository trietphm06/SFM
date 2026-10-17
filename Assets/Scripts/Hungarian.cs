namespace SFM.Core
{
    /// <summary>
    /// Thuật toán Hungarian: gán n hàng -> n cột sao cho tổng chi phí nhỏ nhất (O(n^3)).
    /// Dùng để xếp cầu thủ vào các slot của sơ đồ sao cho tổng điểm phạt lệch vị trí thấp nhất.
    /// </summary>
    public static class Hungarian
    {
        /// <summary>cost[i, j] = chi phí gán hàng i cho cột j. Trả về mảng: hàng -> cột được gán.</summary>
        public static int[] Solve(float[,] cost)
        {
            int n = cost.GetLength(0);
            const float INF = 1e9f;
            var u = new float[n + 1];
            var v = new float[n + 1];
            var p = new int[n + 1];
            var way = new int[n + 1];

            for (int i = 1; i <= n; i++)
            {
                p[0] = i;
                int j0 = 0;
                var minv = new float[n + 1];
                var used = new bool[n + 1];
                for (int j = 0; j <= n; j++) minv[j] = INF;

                do
                {
                    used[j0] = true;
                    int i0 = p[j0], j1 = 0;
                    float delta = INF;
                    for (int j = 1; j <= n; j++)
                    {
                        if (used[j]) continue;
                        float cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                        if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                    }
                    for (int j = 0; j <= n; j++)
                    {
                        if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                        else minv[j] -= delta;
                    }
                    j0 = j1;
                } while (p[j0] != 0);

                do
                {
                    int j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }

            var result = new int[n];
            for (int j = 1; j <= n; j++) result[p[j] - 1] = j - 1;
            return result;
        }
    }
}
