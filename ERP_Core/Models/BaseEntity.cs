namespace ERP_Core.Models
{
    public class BaseEntity
    {
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public bool? Active { get; set; }
    }

    public enum PrefixType
    {
        GRP, CS, STF, CB, ROL, CAT, INS, MST, AT, MEM, IA, SOR, CN, AW, DW, MT, EVT, ATT, NOT, LVL, LC, ORG, DT, M, PRJ, PT, PTCK, D, TSK, TCK, PP, DEP,
        JV, CP, CR, BP, BR, CPV, CRV, BPV, BRV
    }
}
