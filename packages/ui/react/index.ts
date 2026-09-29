// @afk4/ui/react — общие компоненты обеих админок поверх классов кита (kit.css, screen.css).
// Строк внутри нет: каждая подпись приходит пропсом, поэтому кит не зависит от @afk4/i18n и
// одинаково служит Панели и Platform Control.
export { Button, CloseButton, IconButton, type ButtonProps, type ButtonSize, type ButtonVariant, type IconButtonProps } from './Button';
export { RowActions, type RowAction } from './RowActions';
export { Segmented, Tabs, type SegmentOption, type TabItem } from './Tabs';
export { CountChip, FilterChip, StatusBadge, type CountChipProps, type StatusTone } from './chips';
export { EmptyState, LoadFailure, type EmptyNext } from './states';
export { Money, Num } from './numbers';
export { SectionHeader, type HeaderCounts } from './SectionHeader';
export { Inspector, type Fact } from './Inspector';
export { useBlockedReason } from './BlockedReason';
