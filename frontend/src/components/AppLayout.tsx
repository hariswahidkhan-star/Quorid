import { Outlet } from 'react-router-dom';
import { ShellBar } from './ShellBar';

export function AppLayout() {
  return (
    <>
      <ShellBar />
      <Outlet />
    </>
  );
}
