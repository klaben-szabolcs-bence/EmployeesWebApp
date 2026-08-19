import { Component, Input, OnInit } from '@angular/core';
import { Department } from '../../models/department';
import { WebAPIService } from '../../web-api.service';
import { ShowDepartmentComponent } from '../show/show.component';


@Component({
  selector: 'app-add-edit-department',
  standalone: false,
  templateUrl: './add-edit.component.html',
  styleUrls: ['./add-edit.component.css']
})
export class AddEditDepartmentComponent implements OnInit {

  constructor(private api: WebAPIService) { }

  @Input()
  ShowDepComp: ShowDepartmentComponent | undefined;
  DepartmentID: number | undefined;
  DepartmentName: string | undefined;
  @Input()
  modal: any;

  ngOnInit(): void {
    this.DepartmentID = this.ShowDepComp?.department.DepartmentID;
    this.DepartmentName = this.ShowDepComp?.department.DepartmentName;
  }

  addDepartment() {
    if (this.DepartmentID == undefined || this.DepartmentName == undefined) {
      console.log("DepartmentID or DepartmentName is undefined");
      return;
    }
    var newDepartment = { DepartmentID: this.DepartmentID, DepartmentName: this.DepartmentName };
    this.api.addDepartment(newDepartment).subscribe(
      result => {
        this.ShowDepComp?.closeModal(this.modal, "Added");
        this.ShowDepComp?.showSuccess(result.Message);
      }
    );
  }

  updateDepartment() {
    if (this.DepartmentID == undefined || this.DepartmentName == undefined) {
      console.log("DepartmentID or DepartmentName is undefined");
      return;
    }
    var newDepartment = { DepartmentID: this.DepartmentID, DepartmentName: this.DepartmentName };
    this.api.updateDepartment(newDepartment).subscribe(
      result => {
        this.ShowDepComp?.closeModal(this.modal, "Updated");
        this.ShowDepComp?.showSuccess(result.Message);
      }
    );
  }
}
