// ***************************************************************************************
// * SCMAP Loader
// * Copyright Unknown
// * Filename: Layer.cs
// * Source: http://www.hazardx.com/details.php?file=82
// ***************************************************************************************

[System.Serializable]
public class Layer
{

    public string PathTexture;
    public string PathNormalmap;
    public float ScaleTexture;

    public float ScaleNormalmap;
    public Layer()
    {
        this.PathTexture = "";
        this.PathNormalmap = "";
        this.ScaleTexture = 4f;
        this.ScaleNormalmap = 4f;
    }

}