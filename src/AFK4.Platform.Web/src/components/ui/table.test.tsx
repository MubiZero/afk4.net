import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from './table';

it('renders a semantic table with header and cells', () => {
  render(
    <Table>
      <TableHeader><TableRow><TableHead>Имя</TableHead></TableRow></TableHeader>
      <TableBody><TableRow><TableCell>ПК-1</TableCell></TableRow></TableBody>
    </Table>
  );
  expect(screen.getByRole('table')).toBeInTheDocument();
  expect(screen.getByRole('columnheader', { name: 'Имя' })).toBeInTheDocument();
  expect(screen.getByRole('cell', { name: 'ПК-1' })).toBeInTheDocument();
});
