import { Component, OnInit } from '@angular/core';
import { ShowEmployeeComponent } from './show/show.component';

@Component({
  selector: 'app-employee',
  imports: [ShowEmployeeComponent],
  templateUrl: './employee.component.html',
  styleUrls: ['./employee.component.css']
})
export class EmployeeComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

}
