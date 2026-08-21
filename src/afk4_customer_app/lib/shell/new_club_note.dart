import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';

/// «Здесь вы ещё не играли» — состояние клуба, в котором у игрока пока нет счёта.
///
/// Это не ошибка и не пустой список: аккаунт один на всю сеть, а деньги, брони и история у
/// каждого клуба свои и заводятся первым действием. Показывать вместо этого нули значило бы
/// пообещать кошелёк, которого нет, а «не удалось загрузить» — соврать про сбой.
class NewClubNote extends StatelessWidget {
  const NewClubNote({super.key});

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.storefront_outlined, size: 18, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(l.customerClubsNoAccount, style: theme.textTheme.titleMedium),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              l.customerClubsNoAccountHint,
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}
