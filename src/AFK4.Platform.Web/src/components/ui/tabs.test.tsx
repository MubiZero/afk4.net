import { it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from './tabs';

it('shows the active tab content', () => {
  render(
    <Tabs defaultValue="devices">
      <TabsList>
        <TabsTrigger value="devices">Устройства</TabsTrigger>
        <TabsTrigger value="pending">Ожидают</TabsTrigger>
      </TabsList>
      <TabsContent value="devices">Список устройств</TabsContent>
      <TabsContent value="pending">Очередь</TabsContent>
    </Tabs>
  );
  expect(screen.getByText('Список устройств')).toBeInTheDocument();
  expect(screen.getByRole('tab', { name: 'Устройства' })).toHaveAttribute('data-state', 'active');
});
