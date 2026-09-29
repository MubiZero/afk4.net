namespace AFK4.Shared.Contracts.Pos;

/// <summary>
/// Машинные имена отказов по кассе. См. <see cref="Sessions.SessionErrorCodeNames"/> — та же
/// причина: чек мог измениться между тем, что кассир видит на экране, и тем, что уже случилось
/// на сервере.
/// </summary>
public static class PosErrorCodeNames
{
    /// <summary>Аннулировать можно только черновик или ожидающий оплаты чек — этот уже оплачен или аннулирован.</summary>
    public const string NotVoidable = "pos_sale_not_voidable";

    /// <summary>Вернуть деньги можно только за оплаченный чек — этот уже возвращён или ещё не оплачен.</summary>
    public const string NotRefundable = "pos_sale_not_refundable";

    /// <summary>Категорию товара скрыли, пока чек уже собирали, — продать его нельзя.</summary>
    public const string CategoryHidden = "product_category_hidden";
}
