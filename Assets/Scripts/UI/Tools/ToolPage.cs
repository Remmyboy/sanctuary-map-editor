using UnityEngine;

public abstract class ToolPage : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] protected ToolTabGroup tabGroup;
    [SerializeField] protected TabPageIdentifier[] Pages;
    
    // Pages for things a .sanmap doesn't hold: FA teams and extra armies
    // (Map > Armies), FA AI marker chains (Markers > Chains), FA terrain
    // types (Terrain > Type), and FA stratum template/settings files and
    // layer reordering (Textures > Tools). Kept, with their tabs hidden, so
    // page ids and the undo code that switches to them stay valid.
    static readonly string[] SanctuaryHiddenTabs = { "Armies", "Chains", "Type", "Tools" };

    protected int PreviousPage = 0;
    protected int CurrentPage = -1;

    public virtual int GetCurrentPage() => CurrentPage;

    public virtual int PreviousCurrentPage() => PreviousPage;


    protected virtual void Awake()
    {
        // Pages deleted with the FA tools (the markers' AI page) leave null
        // slots at the end of the list; drop them so the rest keep their ids.
        Pages = System.Array.FindAll(Pages, p => p != null);
        CreateTabs();
        for (int i = 0; i < Pages.Length; i++)
            if (System.Array.IndexOf(SanctuaryHiddenTabs, Pages[i].TabName) >= 0)
                tabGroup.SetTabVisible(i, false);
        ChangePage(0);
    }

    private void CreateTabs()
    {
        foreach (var page in Pages)
        {
            tabGroup.AddTab(page.TabName);
        }
    }

    public virtual bool ChangePage(int newPageID)
    {
        if (CurrentPage == newPageID && (CurrentPage != -1 && Pages[CurrentPage].gameObject.activeSelf))
            return false;

        PreviousPage = CurrentPage;
        CurrentPage = newPageID;

        tabGroup.SetSelectedTab(newPageID);
        
        for (int i = 0; i < Pages.Length; i++)
        {
            bool isActivePage = (i == newPageID);
            Pages[i].gameObject.SetActive(isActivePage);
        }

        return true;
    }
}
