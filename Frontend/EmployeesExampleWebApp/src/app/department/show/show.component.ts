import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AddEditDepartmentComponent } from '../add-edit/add-edit.component';
import { Department } from '../../models/department';
import { WebAPIService } from '../../web-api.service';
import { NgbModal, NgbAlert } from '@ng-bootstrap/ng-bootstrap';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';

@Component({
  selector: 'app-show-department',
  imports: [CommonModule, NgbAlert, AddEditDepartmentComponent],
  templateUrl: './show.component.html',
  styleUrls: ['./show.component.css']
})
export class ShowDepartmentComponent implements OnInit {

  constructor(private service: WebAPIService, private modalService: NgbModal) { }

  private _success = new Subject<string>();
  departmentList: Department[] = [];

  ModalTitle = 'Modal Title';
  ActivateAddEditDepartmentComponent = false;
  department: Department = { DepartmentID: 0, DepartmentName: '' };
  closeResult = '';
  successMessage = '';

  @ViewChild('selfClosingAlert') alert: NgbAlert | undefined;

  ngOnInit(): void {
    this.refreshDepartmentList();
    this._success.subscribe((message) => this.successMessage = message);
    this._success.pipe(debounceTime(5000)).subscribe(() => {
      if (this.alert) {
        this.alert.close();
      }
    }
    );
  }

  onAdd(content: any) {
    this.department = { DepartmentID: 0, DepartmentName: '' };
    this.ModalTitle = 'Add Department';
    this.ActivateAddEditDepartmentComponent = true;
    this.modalService.open(content, { ariaLabelledBy: 'modal-add-department', size: 'xl', centered: true }).result.then((result) => {
      this.closeResult = `Closed with: ${result}`;
    }
    );
  }

  onClose(modal: any) {
    this.closeModal(modal, 'Closed');
  }

  public closeModal(modal: any, reason: string) {
    this.ActivateAddEditDepartmentComponent = false;
    this.refreshDepartmentList();
    modal.dismiss(reason);
  }

  public showSuccess(message: string) {
    this._success.next(message);
  }

  refreshDepartmentList() {
    this.service.getDepartments().subscribe(
      result => {
        this.departmentList = result;
      }
    );
  }

  onEdit(department: Department, content: any) {
    this.department = department;
    this.ModalTitle = 'Edit Department';
    this.ActivateAddEditDepartmentComponent = true;
    this.modalService.open(content, { ariaLabelledBy: 'modal-edit-department', size: 'xl', centered: true }).result.then((result) => {
      this.closeResult = `Closed with: ${result}`;
    }
    );
  }

  onDelete(department: Department) {
    if(confirm('Are you sure to delete this department?')) {
      this.service.deleteDepartment(department.DepartmentID).subscribe(
        result => {
          this.showSuccess(result.Message);
          this.refreshDepartmentList();
        }
      );
    }
  }

}
