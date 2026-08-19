import { Component, OnInit } from '@angular/core';
import { ShowDepartmentComponent } from './show/show.component';

@Component({
  selector: 'app-department',
  imports: [ShowDepartmentComponent],
  templateUrl: './department.component.html',
  styleUrls: ['./department.component.css']
})
export class DepartmentComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

}
