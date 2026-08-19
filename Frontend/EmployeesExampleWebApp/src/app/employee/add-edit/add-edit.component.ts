import { Component, Input, OnInit } from '@angular/core';
import { Department } from '../../models/department';
import { WebAPIService } from '../../web-api.service';
import { ShowEmployeeComponent } from '../show/show.component';


@Component({
  selector: 'app-add-edit-employee',
  standalone: false,
  templateUrl: './add-edit.component.html',
  styleUrls: ['./add-edit.component.css']
})
export class AddEditEmployeeComponent implements OnInit {

  constructor(public api: WebAPIService) { }

  @Input()
  ShowEmpComp: ShowEmployeeComponent | undefined;
  EmployeeId: number | undefined;
  EmployeeName: string | undefined;
  Department: string | undefined;
  DateOfJoining: string | undefined;
  PhotoFileName: string | undefined;
  DepartmentsList: string[] = [];
  @Input()
  modal: any;

  ngOnInit(): void {
    this.EmployeeId = this.ShowEmpComp?.employee.EmployeeId;
    this.EmployeeName = this.ShowEmpComp?.employee.EmployeeName;
    this.Department = this.ShowEmpComp?.employee.Department;
    this.DateOfJoining = this.ShowEmpComp?.employee.DateOfJoining;
    this.PhotoFileName = this.ShowEmpComp?.employee.PhotoFileName;
    this.loadDepartments();
  }

  loadDepartments() {
    this.api.getAllDepartmentNames().subscribe(
      result => {
        this.DepartmentsList = result;
      }
    );
  }


  addEmployee() {
    if (this.EmployeeId == undefined || this.EmployeeName == undefined || this.Department == undefined
      || this.DateOfJoining == undefined || this.PhotoFileName == undefined) {
      console.log("One of the employee fields is undefined");
      return;
    }
    var newEmployee = {
      EmployeeId: this.EmployeeId, EmployeeName: this.EmployeeName, Department: this.Department,
      DateOfJoining: this.DateOfJoining, PhotoFileName: this.PhotoFileName
    };
    this.api.addEmployee(newEmployee).subscribe(
      result => {
        this.ShowEmpComp?.closeModal(this.modal, "Added");
        this.ShowEmpComp?.showSuccess(result.Message);
      }
    );
  }

  updateEmployee() {
    if (this.EmployeeId == undefined || this.EmployeeName == undefined || this.Department == undefined
      || this.DateOfJoining == undefined || this.PhotoFileName == undefined) {
      console.log("One of the employee fields is undefined");
      return;
    }
    var newEmployee = {
      EmployeeId: this.EmployeeId, EmployeeName: this.EmployeeName, Department: this.Department,
      DateOfJoining: this.DateOfJoining, PhotoFileName: this.PhotoFileName
    };
    this.api.updateEmployee(newEmployee).subscribe(
      result => {
        this.ShowEmpComp?.closeModal(this.modal, "Updated");
        this.ShowEmpComp?.showSuccess(result.Message);
      }
    );
  }

  uploadPhoto(event: any) {
    var file = event.target.files[0];
    this.api.saveFile(file).subscribe(
      result => {
        console.log(result);
        this.ShowEmpComp?.showSuccess(result.Message);
        this.PhotoFileName = event.target.files[0].name;
      }
    );
  }
}
