import { Routes } from '@angular/router';

import { DepartmentComponent } from './department/department.component';
import { EmployeeComponent } from './employee/employee.component';

export const routes: Routes = [
  { path: '', redirectTo: 'employee', pathMatch: 'full' },
  { path: 'employee', component: EmployeeComponent },
  { path: 'department', component: DepartmentComponent },
  { path: '**', redirectTo: 'employee' },
];
