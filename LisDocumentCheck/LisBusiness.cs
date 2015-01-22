﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using DBUtility;
using FileHelper;
using System.Configuration;
namespace LisDocumentCheck
{
    class LisBusiness:SuperBusiness
    {
        //从lis数据库获取已审核报告记录
        private DataTable GetFromDB(string checkDate)
        {
            DbHelperSQL.connectionstring = ConfigurationManager.ConnectionStrings["LisMSSQLConnectionString"].ConnectionString.ToString();
            string sql;
            string[] QueryCondition = Record.GetLisQueryCondition();
            if (QueryCondition != null)
            {
                sql = @"select sicktypeno,patno,hospitalizedtimes,cname,sectionno,checkdate,serialno,zdy1,paritemname from reportform where CONVERT(varchar(100),receivedate,23)='" + checkDate + "'" + CombineQueryCondition(QueryCondition);
            }
            else
            {
                sql = @"select sicktypeno,patno,hospitalizedtimes,cname,sectionno,checkdate,serialno,zdy1,paritemname from reportform where CONVERT(varchar(100),receivedate,23)='" + checkDate + "'";
            }
            return DbHelperSQL.Query(sql).Tables[0];
        }
        //拼接查询条件
        private string CombineQueryCondition(string[] QueryCondition)
        {
            StringBuilder strCondition = new StringBuilder();
            strCondition.Append(" and sectionno in (");
            strCondition.Append("select distinct sectionno from printform where");
            foreach (string str in QueryCondition)
            {
                strCondition.Append(" printprogram like "+str+" or");
            }
            //
            strCondition.Remove(strCondition.Length - 2, 2);
            strCondition.Append(")");
            return strCondition.ToString();
        }
        protected override void CheckOnDB()
        {
            string checkDate = this.CheckCondition;
            //从数据库中获取
            DataTable dt = GetFromDB(checkDate);
            string checkPath="";
            List<FileNameAttr> temp;
            string fileNameLike;
            MyFoler mf;
            foreach (DataRow dr in dt.Rows)
            {
                if (dr["patno"] == null || dr["patno"].ToString() == "")
                {
                    //未知病人
                    continue;
                }
                if (dr["sicktypeno"] != null && dr["sicktypeno"].ToString() != "")
                {
                    if (Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 1 || Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 4)
                    {
                        //住院
                        if (dr["hospitalizedtimes"] == null || dr["hospitalizedtimes"].ToString() == "")
                        {
                            //住院次数错误
                            continue;
                        }
                        checkPath = Path.Combine(Record.GetLisHosPathRoot(), dr["patno"].ToString(), string.Format("{0:D3}", dr["hospitalizedtimes"]), "lis");
                    }
                    //门诊
                    else if (Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 2 || Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 3)
                    {
                        checkPath = Path.Combine(Record.GetLisClinicPathRoot(), string.Format("{0:yyyyMMdd}", dr["checkdate"]));
                    }
                    else
                    {
                        //未知门类
                        continue;
                    }
                }
                else
                {
                    //未知门类
                    continue;
                }
                //文件名
                fileNameLike = "*" + dr["sectionno"].ToString() + "_" + string.Format("{0:yyyyMMdd}", dr["checkdate"]) + "_" + dr["serialno"].ToString() + "_" + dr["zdy1"].ToString();
                //路径存在
                if (MyFoler.CheckPath(checkPath))
                {
                    //
                    mf = new MyFoler(checkPath);
                    //清洗数据
                    temp = ListOperate(mf.GetSpecificFileNameAttrs(fileNameLike));
                    if (temp.Count == 0)
                    {
                        //没有生成pdf,写入数据库
                        FileNameAttr f = new FileNameAttr();
                        f.SetFileNameString(fileNameLike);
                        f.SectionNo = dr["sectionno"].ToString();
                        f.PatientId = dr["patno"].ToString();
                        f.SerialNo = dr["serialno"].ToString();
                        f.SequenceNo = dr["zdy1"].ToString();
                        f.ReportDate = string.Format("{0:yyyyMMdd}", dr["checkdate"]);
                        AddToDB(f, dr, checkPath, "2", "不存在这样的文件名的文件");
                        //txtResult += "病案号为：" + dr["patno"].ToString() + "的患者:" + dr["cname"].ToString() + ",申请单号为" + dr["serialno"].ToString() + "小组号为：" + dr["sectionno"].ToString() + ",对应报告未生成PDF文件" + "\r\n";
                    }
                    else if (temp.Count == 1)
                    {
                        //有pdf
                        FileNameAttr result = temp[0];
                        AddToDB(result, dr, checkPath, "1", "正常");

                    }
                    else
                    {
                        //生成多个pdf
                        FileNameAttr result = temp[0];
                        AddToDB(result, dr, checkPath, "1", "正常");
                    }
                }
               //路径不存在
                else
                {
                    FileNameAttr f = new FileNameAttr();
                    f.SetFileNameString(fileNameLike);
                    f.SectionNo = dr["sectionno"].ToString();
                    f.PatientId = dr["patno"].ToString();
                    f.SerialNo = dr["serialno"].ToString();
                    f.SequenceNo = dr["zdy1"].ToString();
                    f.ReportDate = string.Format("{0:yyyyMMdd}", dr["checkdate"]);
                    AddToDB(f, dr, checkPath, "10", "不存在对应文件路径");
                    //txtResult += "病案号为：" + dr["patno"].ToString() + "的患者:" + dr["cname"].ToString() + ",申请单号为" + dr["serialno"].ToString() + "小组号为：" + dr["sectionno"].ToString() + "的记录,未有对应文件夹" + "\r\n";
                }
            }
        }
        public override bool IsChecked()
        {
            DbHelperSQL.connectionstring = ConfigurationManager.ConnectionStrings["MyMSSQLConnectionString"].ConnectionString.ToString();
            string strSql ="select * from checkrecord where CONVERT(varchar(100),ReportDate,23)='"+this.CheckCondition+"'";
            return DbHelperSQL.Exists(strSql);
        }
        private void CheckBaseOnFS()
        {
        }
        private List<FileNameAttr> ListOperate(List<FileNameAttr> fileNameList)
        {
            if (fileNameList.Count != 0)
            {
                var sortResult = from items in fileNameList where items.GetLegal() == true orderby items.ReportDate descending select items;
                return sortResult.ToList();
            }
            else
            {
                return fileNameList;
            }
        }
        private bool AddToDB(FileNameAttr result,DataRow dr,string checkPath,string fileStatus,string checkSpec)
        {
            DbHelperSQL.connectionstring = ConfigurationManager.ConnectionStrings["MyMSSQLConnectionString"].ConnectionString.ToString();
            StringBuilder strSql = new StringBuilder();
            strSql.Append("INSERT INTO dbo.CheckRecord(");
            strSql.Append("FileName,FileType,FilePath,FileStatus,CheckDate,CheckSpec,PID,SickType,FileClass,AdmissionDate");
            strSql.Append(",VisitID,ReportCode,DischargeDate,PageSize,CID,SectionNo,ReportDate,SerialNo,ItemSum,CName,ReportName)");
            strSql.Append(" values (");
            strSql.Append("@FileName,@FileType,@FilePath,@FileStatus,@CheckDate,@CheckSpec,@PID,@SickType,@FileClass,@AdmissionDate");
            strSql.Append(",@VisitID,@ReportCode,@DischargeDate,@PageSize,@CID,@SectionNo,@ReportDate,@SerialNo,@ItemSum,@CName,@ReportName)");
            SqlParameter[] parameters = {
					new SqlParameter("@FileName", SqlDbType.VarChar,300),
					new SqlParameter("@FileType", SqlDbType.VarChar,10),
					new SqlParameter("@FilePath", SqlDbType.VarChar),
					new SqlParameter("@FileStatus", SqlDbType.VarChar,10),
					new SqlParameter("@CheckDate", SqlDbType.DateTime),
					new SqlParameter("@CheckSpec", SqlDbType.NVarChar),
                    new SqlParameter("@PID", SqlDbType.VarChar,15),
                    new SqlParameter("@SickType", SqlDbType.VarChar,5),
                    new SqlParameter("@FileClass", SqlDbType.VarChar,10),
                    new SqlParameter("@AdmissionDate", SqlDbType.DateTime),
                    new SqlParameter("@VisitID", SqlDbType.VarChar,5),
                    new SqlParameter("@ReportCode", SqlDbType.VarChar,100),
                    new SqlParameter("@DischargeDate", SqlDbType.DateTime),
                    new SqlParameter("@PageSize", SqlDbType.VarChar,10),
                    new SqlParameter("@CID", SqlDbType.VarChar,18),
                    new SqlParameter("@SectionNo", SqlDbType.VarChar,10),
                    new SqlParameter("@ReportDate", SqlDbType.DateTime),
                    new SqlParameter("@SerialNo", SqlDbType.VarChar,20),
                    new SqlParameter("@ItemSum", SqlDbType.Int),
                    new SqlParameter("@CName", SqlDbType.NVarChar,20),
                    new SqlParameter("@ReportName", SqlDbType.NVarChar,100)
                                        };
            parameters[0].Value = result.GetFileNameString(); //filename
            parameters[1].Value = "pdf";//filetype
            parameters[2].Value = checkPath;//filepath
            parameters[3].Value = fileStatus;//filestatus
            parameters[4].Value = DateTime.Now.Date;//checkdate
            parameters[5].Value = checkSpec;//CheckSpec
            parameters[6].Value = result.PatientId;//PID
            parameters[7].Value = result.ClinicType;//门诊类型
            parameters[8].Value = result.SystemType;//文件类型(lis/his/pacs)
            if (result.AdmissonDate != null && !result.AdmissonDate.Equals("%"))
            {
                parameters[9].Value = DateTime.ParseExact(result.AdmissonDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AllowWhiteSpaces);//入院时间
            }
            parameters[10].Value = result.VisitTimes;//住院次数
            parameters[11].Value = result.DocumentCode;//文件编码
            if (result.DischargeDate != null && !result.DischargeDate.Equals("%"))
            {
                parameters[12].Value = DateTime.ParseExact(result.DischargeDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AllowWhiteSpaces);//出院时间
            }
            parameters[13].Value = result.DocumentType;//文件大小
            parameters[14].Value = result.IdNo;//身份证号
            parameters[15].Value = result.SectionNo;//小组号
            if (result.ReportDate != null && !result.ReportDate.Equals("%"))
            {
                parameters[16].Value = DateTime.ParseExact(result.ReportDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AllowWhiteSpaces);//报告时间
            }
            parameters[17].Value = result.SerialNo;//申请单号
            parameters[18].Value = Convert.ToInt32(result.SequenceNo);//项目数
            parameters[19].Value = dr["cname"].ToString();//病人姓名
            parameters[20].Value = dr["paritemname"].ToString();//打印的名称
            int rows = DbHelperSQL.ExecuteSql(strSql.ToString(), parameters);
            if (rows > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private bool AddToDB(FileNameAttr result, DataRow dr, string checkPath)
        {
            DbHelperSQL.connectionstring = ConfigurationManager.ConnectionStrings["MyMSSQLConnectionString"].ConnectionString.ToString();
            StringBuilder strSql = new StringBuilder();
            strSql.Append("INSERT INTO dbo.PDFRecord(");
            strSql.Append("FileName,FileType,FilePath,CheckDate,PID,SickType,FileClass,AdmissionDate");
            strSql.Append(",VisitID,ReportCode,DischargeDate,CID,SectionNo,ReportDate,SerialNo,ItemSum,CName,ReportName)");
            strSql.Append(" values (");
            strSql.Append("@FileName,@FileType,@FilePath,@CheckDate,@PID,@SickType,@FileClass,@AdmissionDate");
            strSql.Append(",@VisitID,@ReportCode,@DischargeDate,@CID,@SectionNo,@ReportDate,@SerialNo,@ItemSum,@CName,@ReportName)");
            SqlParameter[] parameters = {
					new SqlParameter("@FileName", SqlDbType.VarChar,300),
					new SqlParameter("@FileType", SqlDbType.VarChar,10),
					new SqlParameter("@FilePath", SqlDbType.VarChar),
					new SqlParameter("@CheckDate", SqlDbType.DateTime),
                    new SqlParameter("@PID", SqlDbType.VarChar,15),
                    new SqlParameter("@SickType", SqlDbType.VarChar,5),
                    new SqlParameter("@FileClass", SqlDbType.VarChar,10),
                    new SqlParameter("@AdmissionDate", SqlDbType.DateTime),
                    new SqlParameter("@VisitID", SqlDbType.VarChar,5),
                    new SqlParameter("@ReportCode", SqlDbType.VarChar,100),
                    new SqlParameter("@DischargeDate", SqlDbType.DateTime),
                    new SqlParameter("@CID", SqlDbType.VarChar,18),
                    new SqlParameter("@SectionNo", SqlDbType.VarChar,10),
                    new SqlParameter("@ReportDate", SqlDbType.DateTime),
                    new SqlParameter("@SerialNo", SqlDbType.VarChar,20),
                    new SqlParameter("@ItemSum", SqlDbType.Int),
                    new SqlParameter("@CName", SqlDbType.NVarChar,20),
                    new SqlParameter("@ReportName", SqlDbType.NVarChar,100)
                                        };
            parameters[0].Value = result.GetFileNameString(); //filename
            parameters[1].Value = ".pdf";//filetype
            parameters[2].Value = checkPath;//filepath
            parameters[3].Value = DateTime.Now.Date;//checkdate
            parameters[4].Value = result.PatientId;//PID
            parameters[5].Value = result.ClinicType;//门诊类型
            parameters[6].Value = result.SystemType;//文件类型(lis/his/pacs)
            if (result.AdmissonDate != null && !result.AdmissonDate.Equals("%"))
            {
                parameters[7].Value = DateTime.ParseExact(result.AdmissonDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AllowWhiteSpaces);//入院时间
            }
            parameters[8].Value = result.VisitTimes;//住院次数
            parameters[9].Value = result.DocumentCode;//文件编码
            if (result.DischargeDate != null && !result.DischargeDate.Equals("%"))
            {
                parameters[10].Value = DateTime.ParseExact(result.DischargeDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AllowWhiteSpaces);//出院时间
            }
            parameters[11].Value = result.IdNo;//身份证号
            parameters[12].Value = result.SectionNo;//小组号
            if (result.ReportDate != null && !result.ReportDate.Equals("%"))
            {
                parameters[13].Value = DateTime.ParseExact(result.ReportDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AllowWhiteSpaces);//报告时间
            }
            parameters[14].Value = result.SerialNo;//申请单号
            parameters[15].Value = Convert.ToInt32(result.SequenceNo);//项目数
            parameters[16].Value = dr["cname"].ToString();//病人姓名
            parameters[17].Value = dr["paritemname"].ToString();//打印的名称
            int rows = DbHelperSQL.ExecuteSql(strSql.ToString(), parameters);
            if (rows > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }



        public override List<IResult> LisReport(string serialNo)
        {
            List<string> serialNos = new List<string>();
            serialNos.Add(serialNo);
            return LisReports(serialNos);
        }
        public override List<IResult> LisReports(List<string> serialNos)
        {
            LisReportResult temp = null;
            //申请单表单
            DataTable ReportForm = getReportFrom(serialNos);
            DataTable PDFTable = getPDFTable(serialNos);
            List<IResult> lisResult = new List<IResult>();
            DataRow[] Reportdr;
            DataRow[] PDFdr;
            foreach (string serialNo in serialNos)
            {
                temp = new LisReportResult();
                Reportdr = getRows(ReportForm, serialNo);
                //查看该申请单号在lis中是否存在

                if (Reportdr.Length==0)
                {
                    temp.SerialNo = serialNo;
                    //未有此申请单号
                    temp.ReportStatus = 2;
                    lisResult.Add(temp);
                }
                 //申请号对应一个报告
                else if (Reportdr.Length == 1)
                {
                    PDFdr = getRows(PDFTable, serialNo);
                    //判断该申请单号是否有pdf文件记录
                    if (PDFdr.Length > 0)
                    {
                        //有pdf文件记录
                        DataRow dr = PDFdr[0];

                        temp.FileName = dr["FileName"].ToString() + dr["FileType"].ToString();
                        temp.FilePath = dr["FilePath"].ToString();
                        temp.ReportDate = dr["ReportDate"].ToString();
                        temp.SerialNo = dr["SerialNo"].ToString();
                        temp.SectionNo = dr["SectionNo"].ToString();
                        temp.PrintName = dr["ReportName"].ToString();
                        temp.SickType = dr["SickType"].ToString();
                        //已发布
                        temp.ReportStatus = 0;
                        lisResult.Add(temp);
                    }
                     //没有pdf文件记录
                    else
                    {
                        DataRow dr = Reportdr[0];
                        string checkPath = getCheckPath(dr);
                        //是否有pdf文件
                        if (checkPath != null && checkPath != "")
                        {
                            List<FileNameAttr> fileNameList = getPDFFromFS(checkPath, getFileName(dr));
                            if (fileNameList.Count != 0)
                            {
                                FileNameAttr fna = fileNameList[0];
                                //将已生成pdf文件的记录写入数据库
                                AddToDB(fna, Reportdr[0], checkPath);
                                temp.FileName = fna.GetFileNameString() + ".pdf";
                                temp.FilePath = checkPath;
                                temp.ReportDate = dr["checkdate"].ToString();
                                temp.SerialNo = dr["serialno"].ToString();
                                temp.SectionNo = dr["sectionno"].ToString();
                                temp.PrintName = dr["paritemname"].ToString();
                                temp.SickType = dr["sicktypeno"].ToString();
                                //已发布
                                temp.ReportStatus = 0;
                                lisResult.Add(temp);
                            }
                            //未生成pdf
                            else
                            {
                                temp.ReportDate = dr["checkdate"].ToString();
                                temp.SerialNo = dr["serialno"].ToString();
                                temp.SectionNo = dr["sectionno"].ToString();
                                temp.PrintName = dr["paritemname"].ToString();
                                temp.SickType = dr["sicktypeno"].ToString();
                                //已审核未发布
                                temp.ReportStatus = 1;
                                lisResult.Add(temp);
                            }
                        }
                            //不存在pdf文件路径
                        else 
                        {
                            temp.ReportDate = dr["checkdate"].ToString();
                            temp.SerialNo = dr["serialno"].ToString();
                            temp.SectionNo = dr["sectionno"].ToString();
                            temp.PrintName = dr["paritemname"].ToString();
                            temp.SickType = dr["sicktypeno"].ToString();
                            //已审核未发布
                            temp.ReportStatus = 1;
                            lisResult.Add(temp);
                        }
                    }
                }
                    //一个申请单对应多个报告
                else
                {

                }
            }
            return lisResult;
        }
            //查看mylis是否有pdf记录
            //查看文件系统是否有pdf文件
        private DataTable getReportFrom(List<string> serialNos)
        {
            DbHelperSQL.connectionstring = ConfigurationManager.ConnectionStrings["LisMSSQLConnectionString"].ConnectionString.ToString();
            StringBuilder sqlstr = new StringBuilder();
            sqlstr.Append("select sicktypeno,patno,hospitalizedtimes,cname,sectionno,checkdate,serialno,zdy1,paritemname");
            sqlstr.Append(" from reportform where serialno in(");
            foreach (string serialNo in serialNos)
            {
                sqlstr.Append("'"+serialNo+"',");
            }
            sqlstr.Remove(sqlstr.Length - 1, 1);
            sqlstr.Append(")");
            return DbHelperSQL.Query(sqlstr.ToString()).Tables[0];
        }
        //获取已生成pdf的记录
        private DataTable getPDFTable(List<string> serialNos)
        {
            DbHelperSQL.connectionstring = ConfigurationManager.ConnectionStrings["MyMSSQLConnectionString"].ConnectionString.ToString();
            StringBuilder sqlstr = new StringBuilder();
            sqlstr.Append("SELECT FileName,FileType,FilePath,SickType,SectionNo,ReportDate,SerialNo,ReportName");
            sqlstr.Append(" FROM PDFRecord where serialno in(");
            foreach (string serialNo in serialNos)
            {
                sqlstr.Append("'" + serialNo + "',");
            }
            sqlstr.Remove(sqlstr.Length - 1, 1);
            sqlstr.Append(")");
            return DbHelperSQL.Query(sqlstr.ToString()).Tables[0];
        }
        private DataRow[] getRows(DataTable dt, string serialNo)
        {
            string where = "serialno ='" + serialNo +"'";
            return dt.Select(where);
      
        }
        private List<FileNameAttr> getPDFFromFS(string checkPath,string fileName)
        {
            MyFoler mf = new MyFoler(checkPath);
            return ListOperate(mf.GetSpecificFileNameAttrs(fileName));
        }
        private string getCheckPath(DataRow dr)
        {
            string checkPath ="";
            if (dr["patno"] != null && dr["patno"].ToString() != "" && dr["sicktypeno"] != null && dr["sicktypeno"].ToString() != "")
            {
                if (Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 1 || Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 4)
                {
                    //住院
                    if (dr["hospitalizedtimes"] != null && dr["hospitalizedtimes"].ToString() != "")
                    {
                        checkPath = Path.Combine(Record.GetLisHosPathRoot(), dr["patno"].ToString(), string.Format("{0:D3}", dr["hospitalizedtimes"]), "lis");
                        if (MyFoler.CheckPath(checkPath))
                        {
                            return checkPath;
                        }
                        else
                        {
                            //pdf路径不存在 
                            return "";
                        }
                    }
                    else
                    {
                        //住院次数不存在
                        return null;
                    }
                }
                //门诊
                else if (Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 2 || Int32.Parse(dr["sicktypeno"].ToString().Trim()) == 3)
                {
                    checkPath = Path.Combine(Record.GetLisClinicPathRoot(), string.Format("{0:yyyyMMdd}", dr["checkdate"]));
                    if (MyFoler.CheckPath(checkPath))
                    {
                        return checkPath;
                    }
                    else
                    {
                        //pdf路径不存在
                        return "";
                    }
                }
                else
                {
                    //未知门诊类型
                    return null;
                }
            }
            else
            {
                //病历号、
                return null;
            }
        }
        private string getFileName(DataRow dr)
        {
            return "*" + dr["sectionno"].ToString() + "_" + string.Format("{0:yyyyMMdd}", dr["checkdate"]) + "_" + dr["serialno"].ToString() + "_" + dr["zdy1"].ToString();
        }
    }
}
