namespace WonClick.TypeNameParameter.Models
{
    public sealed class TypeNameParameterElementResult
    {
        public bool HasTargetParameter
        {
            get;
            set;
        }


        public int ItemUpdated
        {
            get;
            set;
        }


        public int MateUpdated
        {
            get;
            set;
        }


        public int SizeUpdated
        {
            get;
            set;
        }


        public bool HadError
        {
            get;
            set;
        }


        public int UpdatedParameterCount
        {
            get
            {
                return
                    ItemUpdated
                    +
                    MateUpdated
                    +
                    SizeUpdated;
            }
        }


        public bool Changed
        {
            get
            {
                return
                    UpdatedParameterCount > 0;
            }
        }
    }


    public sealed class TypeNameParameterBatchResult
    {
        public int TargetCategories
        {
            get;
            set;
        }


        // SK Binding Category까지 통과한 Element
        public int ScannedElements
        {
            get;
            set;
        }


        // Type Name에 ","가 없어서 완전히 Skip된 Element
        public int SkippedNoComma
        {
            get;
            set;
        }


        public int TargetElements
        {
            get;
            set;
        }


        public int UpdatedElements
        {
            get;
            set;
        }


        public int ItemUpdated
        {
            get;
            set;
        }


        public int MateUpdated
        {
            get;
            set;
        }


        public int SizeUpdated
        {
            get;
            set;
        }


        public int ErrorElements
        {
            get;
            set;
        }


        public int TypeCacheEntries
        {
            get;
            set;
        }


        // Service 내부
        // 검색 + 비교 + Parameter.Set 시간
        public long ElapsedMilliseconds
        {
            get;
            set;
        }


        // Transaction.Commit 시간
        public long CommitMilliseconds
        {
            get;
            set;
        }


        // Transaction 시작 ~ Commit 완료까지
        public long TotalMilliseconds
        {
            get;
            set;
        }


        public int UpdatedParameters
        {
            get
            {
                return
                    ItemUpdated
                    +
                    MateUpdated
                    +
                    SizeUpdated;
            }
        }
    }
}