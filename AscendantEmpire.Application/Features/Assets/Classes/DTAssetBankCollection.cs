using AscendantEmpire.FrontendContracts.Assets.GetAssetBanks;

namespace AscendantEmpire.Application.Features.Assets.Classes;

public class DTAssetBankCollection : IAssetBankCollection
{
    public required IReadOnlyCollection<IAssetBankHoldings> Banks { get; set; }
}

public class DTAssetNetworkHoldings : IAssetNetworkHoldings
{
    public required Guid NetworkKey { get; set; }

    public required int SiteAssetBankId { get; set; }

    public required IReadOnlyCollection<IAssetBankHoldings> Banks { get; set; }
}

public class DTAssetBankHoldings : IAssetBankHoldings
{
    public required int AssetBankId { get; set; }

    public required int SettlementId { get; set; }

    public required Guid NetworkKey { get; set; }

    public required IReadOnlyCollection<IAssetBankHolding> Holdings { get; set; }
}

public class DTAssetBankHolding : IAssetBankHolding
{
    public required int AssetId { get; set; }

    public required string AssetCode { get; set; }

    public required string DisplayName { get; set; }

    public required int? AssetGroupId { get; set; }

    public required long Quantity { get; set; }
}
